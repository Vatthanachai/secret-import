using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Drives one import through <see cref="IImportService"/>: Analyze → (init) → confirm → Apply, and maps the outcome to an exit code (ADR-0005, ADR-0008).
/// Normal output goes to <paramref name="console"/>, errors to <paramref name="errorConsole"/>.
/// </summary>
/// <param name="scopeResolver">Picks the shared-id scope when <c>--scope</c> is omitted; defaults to the real file system.</param>
public sealed class ImportFlow(IAnsiConsole console, IAnsiConsole errorConsole, TextReader stdin, IImportService importService, ScopeResolver? scopeResolver = null)
{
    private readonly ScopeResolver scopeResolver = scopeResolver ?? new ScopeResolver();

    public const string StdinDisplayName = "stdin";

    public async Task<int> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCoreAsync(options, cancellationToken);
        }
        catch (ImportException ex)
        {
            WriteError(ex.Message);
            foreach (ProjectRef candidate in ex.Candidates)
            {
                errorConsole.WriteLine($"  {candidate.Name}  {candidate.Path}");
            }

            if (ex.Kind == ImportErrorKind.MultipleProjects)
            {
                errorConsole.WriteLine("Pass one project file to --project.");
            }

            return ExitCodes.FromErrorKind(ex.Kind);
        }
        catch (OperationCanceledException)
        {
            WriteError("Cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WriteError(ex.Message);
            return ExitCodes.EnvironmentError;
        }
    }

    private async Task<int> RunCoreAsync(ImportOptions options, CancellationToken ct)
    {
        if (options.ReadsStdin && !options.DryRun && !options.Yes)
        {
            WriteError("--file - reads the JSON from stdin, so there is no way to confirm interactively. "
                + "Pass --yes (and --init if the project has no UserSecretsId yet), or preview with --dry-run.");
            return ExitCodes.Cancelled;
        }

        JsonInput json = options.ReadsStdin
            ? new JsonInput(StdinDisplayName, await stdin.ReadToEndAsync(ct))
            : JsonInput.FromFile(options.File);
        ImportRequest request = new(options.Project, json, options.Mode, options.Section, scopeResolver.Resolve(options.Project, options.Scope));
        ImportAnalysis analysis = await importService.AnalyzeAsync(request, ct);

        if (options.DryRun)
        {
            AnalysisRenderer.Render(console, analysis, options.ShowValues);
            console.WriteLine("Dry run: nothing was written.");
            return ExitCodes.Success;
        }

        if (!analysis.Plan.HasChanges)
        {
            AnalysisRenderer.Render(console, analysis, options.ShowValues);
            console.WriteLine("No changes to import.");
            return ExitCodes.Success;
        }

        ProjectRef project = analysis.Project.Project;
        if (analysis.NeedsInit && !analysis.CanInit)
        {
            WriteError($"Project '{project.Name}' has no UserSecretsId and can't be initialized because the .NET SDK is unavailable. "
                + $"Install the SDK, or run: dotnet user-secrets init --project \"{project.Path}\"");
            return ExitCodes.EnvironmentError;
        }

        List<string> missingFlags = [];
        if (analysis.NeedsInit && !options.Init)
        {
            missingFlags.Add("--init");
        }

        if (!options.Yes)
        {
            missingFlags.Add("--yes");
        }

        bool canPrompt = console.Profile.Capabilities.Interactive && !options.ReadsStdin;
        if (missingFlags.Count > 0 && !canPrompt)
        {
            AnalysisRenderer.Render(console, analysis, options.ShowValues);
            string reason = options.ReadsStdin ? "stdin is used for the JSON input" : "the session is not interactive";
            WriteError($"Confirmation is required but {reason}. Pass {string.Join(" and ", missingFlags)} to continue.");
            return ExitCodes.Cancelled;
        }

        if (analysis.NeedsInit)
        {
            if (!options.Init && !await ConfirmAsync(
                    $"Project '{project.Name}' has no UserSecretsId. Run 'dotnet user-secrets init'? This edits the project file.", ct))
            {
                WriteError("Cancelled: the project was not initialized.");
                return ExitCodes.Cancelled;
            }

            await importService.InitAsync(project, ct);
            console.WriteLine($"Initialized user secrets for '{project.Name}'.");
            analysis = await importService.AnalyzeAsync(request, ct);
        }

        AnalysisRenderer.Render(console, analysis, options.ShowValues);
        if (!options.Yes && !await ConfirmAsync($"Import into {analysis.SecretsPath ?? project.Name}?", ct))
        {
            WriteError("Cancelled: nothing was written.");
            return ExitCodes.Cancelled;
        }

        ImportResult result = await importService.ApplyAsync(analysis, ct);
        console.WriteLine($"Imported: +{result.Added} added, ~{result.Changed} changed, -{result.Removed} removed.");
        console.WriteLine($"Secrets file: {result.SecretsPath}");
        if (result.BackupPath is not null)
        {
            console.WriteLine($"Backup: {result.BackupPath}");
        }

        return ExitCodes.Success;
    }

    private Task<bool> ConfirmAsync(string question, CancellationToken ct)
    {
        return new ConfirmationPrompt(Markup.Escape(question)) { DefaultValue = false }.ShowAsync(console, ct);
    }

    private void WriteError(string message)
    {
        errorConsole.Write(new Markup("[red]error:[/] "));
        errorConsole.WriteLine(message);
    }
}
