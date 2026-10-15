namespace SecretImport.Core;

/// <summary>Orchestrates the two-phase Analyze → Apply flow shared by the CLI and the desktop app.</summary>
internal sealed class ImportService(
    ProjectLocator locator,
    UserSecretsIdResolver resolver,
    SecretsInitializer initializer,
    JsonFlattener flattener,
    ImportPlanner planner,
    SecretsStore store,
    IDotnetCli dotnet) : IImportService
{
    public async Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default)
    {
        ProjectLocation location = await locator.LocateAsync(path, ct).ConfigureAwait(false);
        List<(ProjectRef Project, SecretsIdResult Id)> resolved = [];
        foreach (ProjectRef project in location.Projects)
        {
            resolved.Add((project, await resolver.ResolveAsync(project, ct).ConfigureAwait(false)));
        }

        ProjectInfo[] projects = [.. resolved.Select(r => new ProjectInfo(r.Project, r.Id.Id, r.Id.Source, FindSharing(r.Project, r.Id.Id, resolved)))];
        return new DiscoveryResult(projects, location.Warnings);
    }

    public async Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProjectLocation location = await locator.LocateAsync(request.ProjectPath, ct).ConfigureAwait(false);
        if (location.Projects.Count > 1)
        {
            throw new ImportException(
                ImportErrorKind.MultipleProjects,
                $"{request.ProjectPath} contains {location.Projects.Count} projects; choose one of them.",
                location.Projects);
        }

        ProjectRef project = location.Projects[0];
        ProjectLocation? scope = request.ScopePath is null ? null : await LocateScopeAsync(request.ScopePath, ct).ConfigureAwait(false);

        // Parse the input before spawning dotnet, so bad JSON fails fast.
        FlattenResult incoming = flattener.Flatten(request.Json.Content, request.Section, request.Json.DisplayName);

        // Resolve every project in the scope once, reusing the target's result when the target is part of the scope.
        List<(ProjectRef Project, SecretsIdResult Id)> scoped = [];
        SecretsIdResult? targetId = null;
        foreach (ProjectRef candidate in scope?.Projects ?? [])
        {
            SecretsIdResult candidateId = await resolver.ResolveAsync(candidate, ct).ConfigureAwait(false);
            scoped.Add((candidate, candidateId));
            if (SamePath(candidate, project))
            {
                targetId = candidateId;
            }
        }

        SecretsIdResult id = targetId ?? await resolver.ResolveAsync(project, ct).ConfigureAwait(false);
        ProjectRef[] sharedWith = FindSharing(project, id.Id, scoped);

        List<ImportWarning> warnings = [.. location.Warnings];
        warnings.AddRange((scope?.Warnings ?? []).Where(w => !warnings.Contains(w)));
        warnings.AddRange(incoming.Warnings);
        if (sharedWith.Length > 0)
        {
            warnings.Add(new ImportWarning(
                ImportWarningKind.SharedSecretsId,
                $"{project.Name} shares its UserSecretsId with {string.Join(", ", sharedWith.Select(static p => p.Name))}. They all use the same secrets.json, so this import changes their secrets too."));
        }

        if (id.FallbackUsed)
        {
            warnings.Add(new ImportWarning(
                ImportWarningKind.XmlFallbackUsed,
                $"MSBuild could not evaluate {project.Name}, so UserSecretsId was read from the project XML. Values set under a Condition or in imported files may be missed."));
        }

        SecretsSnapshot? snapshot = id.Id is null ? null : store.Read(id.Id);
        if (snapshot is not null)
        {
            warnings.AddRange(snapshot.Warnings);
        }

        IReadOnlyDictionary<string, string> existing = snapshot?.Values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool needsInit = id.Id is null;
        bool canInit = needsInit && await dotnet.IsSdkAvailableAsync(ct).ConfigureAwait(false);
        return new ImportAnalysis(
            new ProjectInfo(project, id.Id, id.Source, sharedWith),
            request,
            snapshot?.Path,
            needsInit,
            canInit,
            existing.Count,
            incoming.Values.Count,
            planner.Plan(existing, incoming.Values, request.Mode),
            warnings,
            snapshot?.Hash);
    }

    private async Task<ProjectLocation> LocateScopeAsync(string scopePath, CancellationToken ct)
    {
        string fullPath = string.IsNullOrWhiteSpace(scopePath) ? string.Empty : Path.GetFullPath(scopePath);
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"Scope path not found: '{scopePath}'. Use a folder, .sln or .slnx that exists.");
        }

        // A scope without projects (or whose target lies outside it) is fine: there is just nothing to compare with.
        return await locator.LocateAsync(fullPath, allowEmpty: true, ct).ConfigureAwait(false);
    }

    /// <summary>A shared id means a shared secrets.json (ADR-0006). Compare case-insensitively: on Windows and macOS the folders are the same.</summary>
    private static ProjectRef[] FindSharing(ProjectRef target, string? id, IEnumerable<(ProjectRef Project, SecretsIdResult Id)> others) =>
        id is null
            ? []
            : [.. others
                .Where(o => !SamePath(o.Project, target) && string.Equals(o.Id.Id, id, StringComparison.OrdinalIgnoreCase))
                .Select(static o => o.Project)];

    private static bool SamePath(ProjectRef a, ProjectRef b) => string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);

    public Task InitAsync(ProjectRef project, CancellationToken ct = default) => initializer.InitAsync(project, ct);

    public Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ct.ThrowIfCancellationRequested();
        string? id = analysis.Project.UserSecretsId;
        if (analysis.NeedsInit || id is null)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"{analysis.Project.Project.Name} has no UserSecretsId yet. Call InitAsync, then analyze again.");
        }

        IReadOnlyList<SecretChange> changes = analysis.Plan.Changes;
        int added = changes.Count(static c => c.Kind == SecretChangeKind.Added);
        int changed = changes.Count(static c => c.Kind == SecretChangeKind.Changed);
        int removed = changes.Count(static c => c.Kind == SecretChangeKind.Removed);
        if (!analysis.Plan.HasChanges)
        {
            // Nothing to write (ADR-0003): no backup, no rename.
            return Task.FromResult(new ImportResult(0, 0, 0, store.GetSecretsPath(id), null));
        }

        SecretsWriteResult written = store.Write(id, analysis.Plan.Result, analysis.ExistingFileHash);
        return Task.FromResult(new ImportResult(added, changed, removed, written.Path, written.BackupPath));
    }
}