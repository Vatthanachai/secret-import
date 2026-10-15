using System.Text;

namespace SecretImport.Core.Tests;

/// <summary>ExportAsync and WriteExportAsync (ADR-0015).</summary>
public sealed partial class ImportServiceTests
{
    private const string MixedSecrets = """
        {
          "Db:Conn": "Server=.;Password=p@ss",
          "DB:Port": "5432",
          "Items:0": "first",
          "Items:1:Name": "second",
          "Thai": "รหัสผ่าน <&+>",
          "Empty": "",
          "Logging": { "LogLevel": { "Default": "Warning" } }
        }
        """;

    private static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    // ---- ExportAsync ----

    [Fact]
    public async Task Export_Nested_IsTheDefault_AndReportsProjectPathAndCount()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Db:Conn": "c", "Name": "n", "db:port": "1" }""");

        ExportResult result = await _service.ExportAsync(new ExportRequest(project), Ct);

        Assert.Equal(new ProjectRef(project, "App"), result.Project.Project);
        Assert.Equal("app-id", result.Project.UserSecretsId);
        Assert.Equal(SecretsIdSource.MsBuild, result.Project.Source);
        Assert.Equal(SecretsPath("app-id"), result.SecretsPath);
        Assert.Equal(3, result.KeyCount);
        Assert.Equal(Lines("{", "  \"Db\": {", "    \"Conn\": \"c\",", "    \"port\": \"1\"", "  },", "  \"Name\": \"n\"", "}"), result.Json);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Export_Flat_WritesKeysAsStored_InStoreOrder()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Db:Conn": "c", "Name": "n", "Db": { "Port": "1" } }""");

        ExportResult result = await _service.ExportAsync(new ExportRequest(project, Flat: true), Ct);

        Assert.Equal(Lines("{", "  \"Db:Conn\": \"c\",", "  \"Name\": \"n\",", "  \"Db:Port\": \"1\"", "}"), result.Json);
        Assert.Equal(3, result.KeyCount);
    }

    [Fact]
    public async Task Export_DoesNotWriteAnything()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1" }""");
        byte[] before = File.ReadAllBytes(SecretsPath("app-id"));

        await _service.ExportAsync(new ExportRequest(project), Ct);

        Assert.Equal(before, File.ReadAllBytes(SecretsPath("app-id")));
        Assert.Equal([SecretsPath("app-id")], Directory.GetFiles(Path.GetDirectoryName(SecretsPath("app-id"))!));
        Assert.DoesNotContain(_cli.Calls, static c => c.Arguments[0] == "user-secrets");
    }

    [Theory]
    [InlineData("db")]
    [InlineData(" :DB: ")]
    public async Task Export_Section_IsCaseInsensitive_AndKeysKeepThePrefix(string section)
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Db:Conn": "c", "Dbx": "no", "Db:Port": "1", "Name": "n" }""");

        ExportResult nested = await _service.ExportAsync(new ExportRequest(project, section), Ct);
        ExportResult flat = await _service.ExportAsync(new ExportRequest(project, section, Flat: true), Ct);

        Assert.Equal(Lines("{", "  \"Db\": {", "    \"Conn\": \"c\",", "    \"Port\": \"1\"", "  }", "}"), nested.Json);
        Assert.Equal(Lines("{", "  \"Db:Conn\": \"c\",", "  \"Db:Port\": \"1\"", "}"), flat.Json);
        Assert.Equal(2, nested.KeyCount);
        Assert.Equal(2, flat.KeyCount);
    }

    [Fact]
    public async Task Export_MissingSection_IsInvalidInput()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Db:Conn": "secret-value" }""");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(project, "Missing"), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("'Missing'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_SectionWithoutASecretsFile_IsInvalidInput()
    {
        string project = AddProject("App/App.csproj", "app-id");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(project, "Db"), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public async Task Export_IdWithoutASecretsFile_IsAnEmptyObject()
    {
        string project = AddProject("App/App.csproj", "app-id");

        ExportResult result = await _service.ExportAsync(new ExportRequest(project), Ct);

        Assert.Equal("{}\n", result.Json);
        Assert.Equal(0, result.KeyCount);
        Assert.Equal(SecretsPath("app-id"), result.SecretsPath);
        Assert.False(File.Exists(SecretsPath("app-id")));
    }

    [Fact]
    public async Task Export_ProjectWithoutUserSecretsId_IsInvalidInput_SayingItIsNotInitialized()
    {
        string project = AddProject("App/App.csproj");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(project), Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("App", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not initialized", ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_secretsRoot));
    }

    [Fact]
    public async Task Export_FolderWithSeveralProjects_IsMultipleProjects()
    {
        string a = AddProject("A/A.csproj", "a-id");
        string b = AddProject("B/B.csproj", "b-id");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(_dir), Ct));

        Assert.Equal(ImportErrorKind.MultipleProjects, ex.Kind);
        Assert.Equal([a, b], ex.Candidates.Select(static p => p.Path));
    }

    [Fact]
    public async Task Export_NothingFound_IsNoProjectFound()
    {
        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(_dir), Ct));

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
    }

    [Fact]
    public async Task Export_KeyWithValueAndChildren_IsInvalidInput_ButFlatWorks()
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "Db": "secret-1", "Db:Conn": "secret-2" }""");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.ExportAsync(new ExportRequest(project), Ct));
        ExportResult flat = await _service.ExportAsync(new ExportRequest(project, Flat: true), Ct);

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("--flat", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, flat.KeyCount);
    }

    [Fact]
    public async Task Export_PassesProjectWarningsThrough()
    {
        string app = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1" }""");
        string sln = AddSolution("All.slnx", "App/App.csproj", "Gone/Gone.csproj");

        ExportResult result = await _service.ExportAsync(new ExportRequest(sln), Ct);

        Assert.Equal(app, result.Project.Project.Path);
        Assert.Equal(ImportWarningKind.MissingProjectFile, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task Export_WithoutSdk_UsesXmlFallback_AndWarnsAboutIt()
    {
        string project = AddProject("App/App.csproj");
        File.WriteAllText(project, "<Project><PropertyGroup><UserSecretsId>xml-id</UserSecretsId></PropertyGroup></Project>");
        WriteSecrets("xml-id", """{ "A": "1" }""");

        ExportResult result = await Create(FakeDotnetCli.Unavailable()).ExportAsync(new ExportRequest(project), Ct);

        Assert.Equal(SecretsIdSource.XmlFallback, result.Project.Source);
        Assert.Equal(1, result.KeyCount);
        Assert.Equal(ImportWarningKind.XmlFallbackUsed, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task Export_Unix_InsecureSecretsFile_PassesTheReadWarningThrough()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not apply on Windows.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", """{ "A": "1" }""");
        File.SetUnixFileMode(SecretsPath("app-id"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

        ExportResult result = await _service.ExportAsync(new ExportRequest(project), Ct);

        Assert.Equal(ImportWarningKind.InsecureFilePermissions, Assert.Single(result.Warnings).Kind);
    }

    [Fact]
    public async Task Export_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ExportAsync(null!, Ct));
    }

    // ---- Round trip: export, then import the output again ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_RoundTrip_ThroughTheFlattenerAndPlanner_ChangesNothing(bool flat)
    {
        string project = AddProject("App/App.csproj", "app-id");
        WriteSecrets("app-id", MixedSecrets);
        IReadOnlyDictionary<string, string> stored = _store.Read("app-id").Values;

        ExportResult result = await _service.ExportAsync(new ExportRequest(project, Flat: flat), Ct);
        FlattenResult reimported = new JsonFlattener().Flatten(result.Json, section: null, "export.json");
        ImportPlan plan = new ImportPlanner().Plan(stored, reimported.Values, ImportMode.Replace);

        Assert.Equal(stored.Count, result.KeyCount);
        Assert.Equal(stored.Count, reimported.Values.Count);
        Assert.Empty(reimported.Warnings);
        Assert.All(plan.Changes, static c => Assert.Equal(SecretChangeKind.Unchanged, c.Kind));
        Assert.False(plan.HasChanges);
        Assert.All(stored, p => Assert.Equal(p.Value, reimported.Values[p.Key]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_RoundTrip_ThroughWriteAndImportIntoAnotherProject_KeepsEveryValue(bool flat)
    {
        string source = AddProject("Source/Source.csproj", "source-id");
        string target = AddProject("Target/Target.csproj", "target-id");
        WriteSecrets("source-id", MixedSecrets);
        string file = Path.Combine(_dir, "export.json");

        ExportResult exported = await _service.ExportAsync(new ExportRequest(source, Flat: flat), Ct);
        await _service.WriteExportAsync(file, exported.Json, overwrite: false, Ct);
        ImportAnalysis analysis = await _service.AnalyzeAsync(new ImportRequest(target, JsonInput.FromFile(file), ImportMode.Overwrite, null), Ct);
        await _service.ApplyAsync(analysis, Ct);

        IReadOnlyDictionary<string, string> original = _store.Read("source-id").Values;
        IReadOnlyDictionary<string, string> copy = _store.Read("target-id").Values;
        Assert.Equal(original.Count, copy.Count);
        Assert.All(original, p => Assert.Equal(p.Value, copy[p.Key]));
    }

    // ---- WriteExportAsync ----

    [Fact]
    public async Task WriteExport_NewFile_IsUtf8WithoutBom_ExactlyAsGiven()
    {
        string file = Path.Combine(_dir, "out.json");
        const string json = "{\n  \"Thai\": \"รหัส\"\n}\n";

        await _service.WriteExportAsync(file, json, overwrite: false, Ct);

        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(Encoding.UTF8.GetBytes(json), bytes);
        Assert.Equal([file], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task WriteExport_RelativePath_IsResolvedAgainstTheCurrentDirectory()
    {
        string file = Path.Combine(_dir, "relative.json");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, file);

        await _service.WriteExportAsync(relative, "{}\n", overwrite: false, Ct);

        Assert.Equal("{}\n", File.ReadAllText(file));
    }

    [Fact]
    public async Task WriteExport_ExistingFile_WithoutOverwrite_IsInvalidInput_AndLeavesItAlone()
    {
        string file = Path.Combine(_dir, "out.json");
        File.WriteAllText(file, "keep me");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.WriteExportAsync(file, "{ \"A\": \"secret-value\" }\n", overwrite: false, Ct));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains(file, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", ex.Message, StringComparison.Ordinal);
        Assert.Equal("keep me", File.ReadAllText(file));
        Assert.Equal([file], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task WriteExport_ExistingFile_WithOverwrite_ReplacesIt_WithoutABackup()
    {
        string file = Path.Combine(_dir, "out.json");
        File.WriteAllText(file, "old");

        await _service.WriteExportAsync(file, "{}\n", overwrite: true, Ct);

        Assert.Equal("{}\n", File.ReadAllText(file));
        Assert.Equal([file], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task WriteExport_MissingFolder_IsIoFailure()
    {
        string file = Path.Combine(_dir, "missing", "out.json");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.WriteExportAsync(file, "{}\n", overwrite: false, Ct));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.Contains(file, ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_dir, "missing")));
    }

    [Fact]
    public async Task WriteExport_PathIsAFolder_IsIoFailure_AndLeavesNoTempFile()
    {
        string folder = Path.Combine(_dir, "folder");
        Directory.CreateDirectory(folder);

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => _service.WriteExportAsync(folder, "{}\n", overwrite: true, Ct));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.Empty(Directory.GetFiles(_dir));
        Assert.True(Directory.Exists(folder));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task WriteExport_NoPath_Throws(string? path)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.WriteExportAsync(path!, "{}\n", overwrite: false, Ct));
    }

    [Fact]
    public async Task WriteExport_NullJson_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.WriteExportAsync(Path.Combine(_dir, "out.json"), null!, overwrite: false, Ct));
    }

    [Fact]
    public async Task WriteExport_Cancelled_WritesNothing()
    {
        string file = Path.Combine(_dir, "out.json");
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.WriteExportAsync(file, "{}\n", overwrite: false, cts.Token));

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task WriteExport_Unix_NewFile_Is600()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not apply on Windows.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string file = Path.Combine(_dir, "out.json");

        await _service.WriteExportAsync(file, "{}\n", overwrite: false, Ct);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task WriteExport_Unix_Overwrite_KeepsTheExistingMode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not apply on Windows.");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        string file = Path.Combine(_dir, "out.json");
        File.WriteAllText(file, "old");
        File.SetUnixFileMode(file, mode);

        await _service.WriteExportAsync(file, "{}\n", overwrite: true, Ct);

        Assert.Equal(mode, File.GetUnixFileMode(file));
        Assert.Equal("{}\n", File.ReadAllText(file));
    }
}