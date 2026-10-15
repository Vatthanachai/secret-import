using System.Text.Json;

namespace SecretImport.Core.Tests;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("secret-import-service-").FullName;
    private readonly string _secretsRoot;
    private readonly Dictionary<string, string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly FakeDotnetCli _cli = new();
    private readonly SecretsStore _store;
    private readonly ImportService _service;

    public ImportServiceTests()
    {
        _secretsRoot = Path.Combine(_dir, "usersecrets");
        _store = new SecretsStore(id => Path.Combine(_secretsRoot, id, "secrets.json"));
        _cli.Handler = args => args[0] == "msbuild"
            ? new DotnetResult(0, _ids.GetValueOrDefault(args[1], string.Empty), string.Empty)
            : new DotnetResult(0, string.Empty, string.Empty);
        _service = Create(_cli);
    }

    private ImportService Create(IDotnetCli cli) =>
        new(new ProjectLocator(), new UserSecretsIdResolver(cli), new SecretsInitializer(cli), new JsonFlattener(), new ImportPlanner(), _store, cli);

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string AddProject(string relative, string? id = null)
    {
        string path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        if (id is not null)
        {
            _ids[path] = id;
        }

        return path;
    }

    private string SecretsPath(string id) => Path.Combine(_secretsRoot, id, "secrets.json");

    private void WriteSecrets(string id, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath(id))!);
        File.WriteAllText(SecretsPath(id), json);
        if (!OperatingSystem.IsWindows())
        {
            // Like a file written by this tool; umask would otherwise give 644 and trigger the ADR-0009 warning.
            File.SetUnixFileMode(SecretsPath(id), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static ImportRequest Request(string project, string json, ImportMode mode = ImportMode.Overwrite, string? section = null, string? scope = null) =>
        new(project, new JsonInput("input.json", json), mode, section, scope);

    private string AddSolution(string relative, params string[] projectPaths)
    {
        string path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string entries = string.Concat(projectPaths.Select(static p => $"<Project Path=\"{p}\" />"));
        File.WriteAllText(path, $"<Solution>{entries}</Solution>");
        return path;
    }

    // ---- DiscoverProjectsAsync ----

    [Fact]
    public async Task Discover_ReturnsEachProjectWithItsId_AndFlagsSharedIds()
    {
        string a = AddProject("A/A.csproj", "shared");
        string b = AddProject("B/B.csproj", "SHARED");
        string c = AddProject("C/C.csproj", "own");
        string d = AddProject("D/D.csproj");

        DiscoveryResult result = await _service.DiscoverProjectsAsync(_dir, Ct);
        IReadOnlyList<ProjectInfo> projects = result.Projects;

        Assert.Equal([a, b, c, d], projects.Select(static p => p.Project.Path));
        Assert.Equal("shared", projects[0].UserSecretsId);
        Assert.Equal(SecretsIdSource.MsBuild, projects[0].Source);
        Assert.Equal([b], projects[0].SharedWith.Select(static p => p.Path));
        Assert.Equal([a], projects[1].SharedWith.Select(static p => p.Path));
        Assert.Empty(projects[2].SharedWith);
        Assert.Null(projects[3].UserSecretsId);
        Assert.Equal(SecretsIdSource.None, projects[3].Source);
        Assert.Empty(projects[3].SharedWith);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Discover_SolutionWithAMissingProject_ReturnsTheWarning()
    {
        string app = AddProject("App/App.csproj", "app-id");
        string sln = AddSolution("All.slnx", "App/App.csproj", "Gone/Gone.csproj");

        DiscoveryResult result = await _service.DiscoverProjectsAsync(sln, Ct);

        Assert.Equal(app, Assert.Single(result.Projects).Project.Path);
        ImportWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ImportWarningKind.MissingProjectFile, warning.Kind);
        Assert.Contains("Gone.csproj", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discover_NothingFound_IsNoProjectFound()
    {
        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.DiscoverProjectsAsync(_dir, Ct));

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
    }

    // ---- AnalyzeAsync ----

    [Fact]
    public async Task Analyze_InitializedProject_DiffsAgainstTheStore()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1", "B": "old" }""");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "B": "new", "C": "3" }"""), Ct);

        Assert.Equal(new ProjectRef(project, "App"), analysis.Project.Project);
        Assert.Equal("app-id", analysis.Project.UserSecretsId);
        Assert.Equal(SecretsIdSource.MsBuild, analysis.Project.Source);
        Assert.Empty(analysis.Project.SharedWith);
        Assert.Equal(SecretsPath("app-id"), analysis.SecretsPath);
        Assert.False(analysis.NeedsInit);
        Assert.False(analysis.CanInit);
        Assert.Equal(2, analysis.ExistingCount);
        Assert.Equal(2, analysis.IncomingCount);
        Assert.Equal(SecretsStore.ComputeHash(File.ReadAllBytes(SecretsPath("app-id"))), analysis.ExistingFileHash);
        Assert.Equal([SecretChangeKind.Changed, SecretChangeKind.Added], analysis.Plan.Changes.Select(static c => c.Kind));
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public async Task Analyze_Unix_GroupReadableSecretsFile_IsWarnedAbout()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not apply on Windows.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1" }""");
        File.SetUnixFileMode(SecretsPath("app-id"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "2" }"""), Ct);

        Assert.Equal(ImportWarningKind.InsecureFilePermissions, Assert.Single(analysis.Warnings).Kind);
    }

    [Fact]
    public async Task Analyze_InitializedProjectWithoutSecretsFile_HasNoHash()
    {
        string project = AddProject("App/App.csproj", "fresh-id");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);

        Assert.False(analysis.NeedsInit);
        Assert.Equal(SecretsPath("fresh-id"), analysis.SecretsPath);
        Assert.Null(analysis.ExistingFileHash);
        Assert.Equal(0, analysis.ExistingCount);
    }

    [Fact]
    public async Task Analyze_UninitializedProject_NeedsInit_AndPlansAgainstAnEmptyStore()
    {
        string project = AddProject("App/App.csproj");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }""", ImportMode.Replace), Ct);

        Assert.True(analysis.NeedsInit);
        Assert.True(analysis.CanInit);
        Assert.Null(analysis.SecretsPath);
        Assert.Null(analysis.ExistingFileHash);
        Assert.Null(analysis.Project.UserSecretsId);
        Assert.Equal(SecretsIdSource.None, analysis.Project.Source);
        Assert.Equal(SecretChangeKind.Added, Assert.Single(analysis.Plan.Changes).Kind);
    }

    [Fact]
    public async Task Analyze_WithoutSdk_UsesXmlFallback_WarnsAboutIt_AndCannotInit()
    {
        string project = AddProject("App/App.csproj");
        string withId = AddProject("Other/Other.csproj");
        File.WriteAllText(withId, "<Project><PropertyGroup><UserSecretsId>xml-id</UserSecretsId></PropertyGroup></Project>");
        ImportService service = Create(FakeDotnetCli.Unavailable());

        ImportAnalysis uninitialized = await service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);
        ImportAnalysis initialized = await service.AnalyzeAsync(Request(withId, """{ "A": "1" }"""), Ct);

        Assert.True(uninitialized.NeedsInit);
        Assert.False(uninitialized.CanInit);
        Assert.Contains(uninitialized.Warnings, static w => w.Kind == ImportWarningKind.XmlFallbackUsed);
        Assert.Equal("xml-id", initialized.Project.UserSecretsId);
        Assert.Equal(SecretsIdSource.XmlFallback, initialized.Project.Source);
        Assert.False(initialized.NeedsInit);
        ImportWarning warning = Assert.Single(initialized.Warnings);
        Assert.Equal(ImportWarningKind.XmlFallbackUsed, warning.Kind);
        Assert.Contains("Condition", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_AppliesTheSection_AndCollectsNullWarnings()
    {
        string project = AddProject("App/App.csproj", "app-id");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "S": { "A": "1", "N": null }, "Other": "x" }""", section: "S"), Ct);

        Assert.Equal(1, analysis.IncomingCount);
        Assert.Equal("S:A", Assert.Single(analysis.Plan.Changes).Key);
        Assert.Equal(ImportWarningKind.NullValueSkipped, Assert.Single(analysis.Warnings).Kind);
        Assert.Equal("S", analysis.Request.Section);
    }

    [Fact]
    public async Task Analyze_InvalidJson_StopsBeforeRunningDotnet()
    {
        string project = AddProject("App/App.csproj", "app-id");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(project, "[1]"), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Empty(_cli.Calls);
    }

    [Fact]
    public async Task Analyze_FolderWithSeveralProjects_IsMultipleProjects_WithCandidates()
    {
        string a = AddProject("A/A.csproj", "a");
        string b = AddProject("B/B.csproj", "b");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(_dir, "{}"), Ct));

        Assert.Equal(ImportErrorKind.MultipleProjects, ex.Kind);
        Assert.Equal([a, b], ex.Candidates.Select(static p => p.Path));
    }

    [Fact]
    public async Task Analyze_SolutionWithAMissingProject_CarriesTheWarning()
    {
        string project = AddProject("App/App.csproj", "app-id");
        string sln = Path.Combine(_dir, "All.slnx");
        File.WriteAllText(sln, """<Solution><Project Path="App/App.csproj" /><Project Path="Gone/Gone.csproj" /></Solution>""");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(sln, """{ "A": "1" }"""), Ct);

        Assert.Equal(project, analysis.Project.Project.Path);
        Assert.Equal(ImportWarningKind.MissingProjectFile, Assert.Single(analysis.Warnings).Kind);
    }

    // ---- AnalyzeAsync with ScopePath (ADR-0013) ----

    [Fact]
    public async Task Analyze_WithoutScope_DoesNotLookAtOtherProjects()
    {
        string a = AddProject("A/A.csproj", "shared");
        AddProject("B/B.csproj", "shared");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, """{ "A": "1" }"""), Ct);

        Assert.Empty(analysis.Project.SharedWith);
        Assert.DoesNotContain(analysis.Warnings, static w => w.Kind == ImportWarningKind.SharedSecretsId);
        Assert.Single(_cli.Calls, static c => c.Arguments[0] == "msbuild");
    }

    [Fact]
    public async Task Analyze_WithFolderScope_FillsSharedWith_CaseInsensitively_AndWarns()
    {
        string a = AddProject("A/A.csproj", "shared-id");
        string b = AddProject("B/B.csproj", "SHARED-ID");
        AddProject("C/C.csproj", "own");
        AddProject("D/D.csproj");
        WriteSecrets("shared-id", """{ "Password": "hunter2" }""");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, """{ "Password": "s3cr3t-value" }""", scope: _dir), Ct);

        Assert.Equal([b], analysis.Project.SharedWith.Select(static p => p.Path));
        ImportWarning warning = Assert.Single(analysis.Warnings);
        Assert.Equal(ImportWarningKind.SharedSecretsId, warning.Kind);
        Assert.Contains("B", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-value", warning.Message, StringComparison.Ordinal);
        // Each project is resolved once: the target is not resolved again as part of the scope.
        Assert.Equal(4, _cli.Calls.Count(static c => c.Arguments[0] == "msbuild"));
    }

    [Fact]
    public async Task Analyze_WithScope_ListsEverySharingProject_InTheWarning()
    {
        string a = AddProject("A/A.csproj", "shared");
        AddProject("B/B.csproj", "shared");
        AddProject("C/C.csproj", "shared");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: _dir), Ct);

        Assert.Equal(["B", "C"], analysis.Project.SharedWith.Select(static p => p.Name));
        string message = Assert.Single(analysis.Warnings).Message;
        Assert.Contains("B", message, StringComparison.Ordinal);
        Assert.Contains("C", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WithScope_WhenNothingIsShared_HasNoWarning()
    {
        string a = AddProject("A/A.csproj", "a");
        AddProject("B/B.csproj", "b");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: _dir), Ct);

        Assert.Empty(analysis.Project.SharedWith);
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public async Task Analyze_WithScope_UninitializedTarget_SharesWithNobody()
    {
        string a = AddProject("A/A.csproj");
        AddProject("B/B.csproj");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: _dir), Ct);

        Assert.True(analysis.NeedsInit);
        Assert.Empty(analysis.Project.SharedWith);
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public async Task Analyze_TargetOutsideTheScope_IsComparedWithTheScopeProjects()
    {
        string target = AddProject("Outside/Target.csproj", "shared");
        string inside = AddProject("Scope/In/In.csproj", "shared");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(target, "{}", scope: Path.Combine(_dir, "Scope")), Ct);

        Assert.Equal([inside], analysis.Project.SharedWith.Select(static p => p.Path));
        Assert.Equal(ImportWarningKind.SharedSecretsId, Assert.Single(analysis.Warnings).Kind);
    }

    [Fact]
    public async Task Analyze_ScopeIsTheTargetProjectItself_SharesWithNobody()
    {
        string a = AddProject("A/A.csproj", "shared");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: a), Ct);

        Assert.Empty(analysis.Project.SharedWith);
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public async Task Analyze_WithSolutionScope_MergesItsWarnings_AndFindsSharedIds()
    {
        string a = AddProject("A/A.csproj", "shared");
        string b = AddProject("B/B.csproj", "shared");
        string sln = AddSolution("All.slnx", "A/A.csproj", "B/B.csproj", "Gone/Gone.csproj");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: sln), Ct);

        Assert.Equal([b], analysis.Project.SharedWith.Select(static p => p.Path));
        Assert.Equal(
            [ImportWarningKind.SharedSecretsId, ImportWarningKind.MissingProjectFile],
            analysis.Warnings.Select(static w => w.Kind).Order());
    }

    [Fact]
    public async Task Analyze_TargetIsTheScopeSolution_KeepsItsMissingProjectWarningOnce()
    {
        AddProject("App/App.csproj", "app-id");
        string sln = AddSolution("All.slnx", "App/App.csproj", "Gone/Gone.csproj");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(sln, "{}", scope: sln), Ct);

        Assert.Equal(ImportWarningKind.MissingProjectFile, Assert.Single(analysis.Warnings).Kind);
    }

    [Fact]
    public async Task Analyze_ScopeWithoutProjects_IsNotAnError()
    {
        string a = AddProject("A/A.csproj", "shared");
        string empty = Directory.CreateDirectory(Path.Combine(_dir, "Empty")).FullName;

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: empty), Ct);

        Assert.Empty(analysis.Project.SharedWith);
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public async Task Analyze_ScopeSolutionWithOnlyMissingProjects_KeepsItsWarnings()
    {
        string a = AddProject("A/A.csproj", "shared");
        string sln = AddSolution("Old.slnx", "Gone/Gone.csproj");

        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(a, "{}", scope: sln), Ct);

        Assert.Empty(analysis.Project.SharedWith);
        Assert.Equal(ImportWarningKind.MissingProjectFile, Assert.Single(analysis.Warnings).Kind);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("does-not-exist.slnx")]
    [InlineData(" ")]
    [InlineData("")]
    public async Task Analyze_ScopeThatDoesNotExist_IsInvalidInput(string scope)
    {
        string a = AddProject("A/A.csproj", "a");
        string scopePath = string.IsNullOrWhiteSpace(scope) ? scope : Path.Combine(_dir, scope);

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(a, "{}", scope: scopePath), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Empty(_cli.Calls);
    }

    [Fact]
    public async Task Analyze_UnsupportedScopeFile_IsInvalidInput()
    {
        string a = AddProject("A/A.csproj", "a");
        string txt = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(txt, "x");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(a, "{}", scope: txt), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public async Task Analyze_WithScope_InvalidJson_StopsBeforeRunningDotnet()
    {
        string a = AddProject("A/A.csproj", "shared");
        AddProject("B/B.csproj", "shared");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(a, "[1]", scope: _dir), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Empty(_cli.Calls);
    }

    [Fact]
    public async Task Analyze_CorruptSecretsFile_IsIoFailure()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", "{ not json");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
    }

    [Fact]
    public async Task Analyze_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.AnalyzeAsync(null!, Ct));
    }

    // ---- InitAsync ----

    [Fact]
    public async Task Init_RunsUserSecretsInit()
    {
        string project = AddProject("App/App.csproj");

        await _service.InitAsync(new ProjectRef(project, "App"), Ct);

        Assert.Contains(_cli.Calls, c => c.Arguments.SequenceEqual(["user-secrets", "init", "--project", project]));
    }

    [Fact]
    public async Task Init_WithoutSdk_IsSdkUnavailable()
    {
        ImportService service = Create(FakeDotnetCli.Unavailable());

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => service.InitAsync(new ProjectRef(Path.Combine(_dir, "A.csproj"), "A"), Ct));

        Assert.Equal(ImportErrorKind.SdkUnavailable, ex.Kind);
    }

    // ---- ApplyAsync ----

    [Fact]
    public async Task Apply_WritesThePlannedResult_WithBackup_AndReportsCounts()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Keep": "k", "Change": "old", "Drop": "d" }""");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "Keep": "k", "Change": "new", "Add": "a" }""", ImportMode.Replace), Ct);

        ImportResult result = await _service.ApplyAsync(analysis, Ct);

        Assert.Equal(new ImportResult(1, 1, 1, SecretsPath("app-id"), SecretsPath("app-id") + ".bak"), result);
        using JsonDocument written = JsonDocument.Parse(File.ReadAllText(SecretsPath("app-id")));
        Assert.Equal(["Keep", "Change", "Add"], written.RootElement.EnumerateObject().Select(static p => p.Name));
        Assert.Equal("new", written.RootElement.GetProperty("Change").GetString());
        Assert.Contains("\"Drop\"", File.ReadAllText(result.BackupPath!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_NewSecretsFile_HasNoBackup()
    {
        string project = AddProject("App/App.csproj", "new-id");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);

        ImportResult result = await _service.ApplyAsync(analysis, Ct);

        Assert.Equal(new ImportResult(1, 0, 0, SecretsPath("new-id"), null), result);
        Assert.True(File.Exists(SecretsPath("new-id")));
    }

    [Fact]
    public async Task Apply_WithoutChanges_DoesNotWrite()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{"A":"1"}""");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);

        ImportResult result = await _service.ApplyAsync(analysis, Ct);

        Assert.Equal(new ImportResult(0, 0, 0, SecretsPath("app-id"), null), result);
        Assert.Equal("""{"A":"1"}""", File.ReadAllText(SecretsPath("app-id")));
        Assert.False(File.Exists(SecretsPath("app-id") + ".bak"));
    }

    [Fact]
    public async Task Apply_WhenTheFileChangedAfterAnalyze_IsConcurrentModification()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1" }""");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "2" }"""), Ct);
        WriteSecrets("app-id", """{ "A": "changed meanwhile" }""");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ApplyAsync(analysis, Ct));

        Assert.Equal(ImportErrorKind.ConcurrentModification, ex.Kind);
    }

    [Fact]
    public async Task Apply_WhenTheProjectNeedsInit_IsInvalidInput()
    {
        string project = AddProject("App/App.csproj");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ApplyAsync(analysis, Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("InitAsync", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_NullAnalysis_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ApplyAsync(null!, Ct));
    }

    [Fact]
    public async Task Apply_WhenCancelled_DoesNotWrite()
    {
        string project = AddProject("App/App.csproj", "app-id");
        ImportAnalysis analysis = await _service.AnalyzeAsync(Request(project, """{ "A": "1" }"""), Ct);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ApplyAsync(analysis, cts.Token));

        Assert.False(File.Exists(SecretsPath("app-id")));
    }
}