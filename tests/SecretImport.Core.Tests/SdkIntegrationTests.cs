namespace SecretImport.Core.Tests;

/// <summary>A throw-away SDK-style project tree in a temp dir, isolated from any Directory.Build.* above it.</summary>
internal sealed class TempProjectTree : IDisposable
{
    public TempProjectTree()
    {
        Root = Directory.CreateTempSubdirectory("secret-import-sdk-").FullName;
        // Stop MSBuild from importing Directory.Build.* files that may exist above the temp folder.
        File.WriteAllText(Path.Combine(Root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(Root, "Directory.Build.targets"), "<Project />");
    }

    public string Root { get; }

    public ProjectRef AddProject(string relative, string extraProperties = "")
    {
        string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {extraProperties}
              </PropertyGroup>
            </Project>
            """);
        return new ProjectRef(path, Path.GetFileNameWithoutExtension(path));
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}

[Trait("Category", "Integration")]
public sealed class SdkIntegrationTests : IDisposable
{
    private readonly TempProjectTree _tree = new();
    private readonly DotnetCli _cli = new();

    public void Dispose()
    {
        _tree.Dispose();
    }

    [Fact]
    public async Task Resolver_ProjectWithoutId_IsNone_ViaMsBuild()
    {
        ProjectRef project = _tree.AddProject("App/App.csproj");

        SecretsIdResult result = await new UserSecretsIdResolver(_cli).ResolveAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: false), result);
    }

    [Fact]
    public async Task Resolver_EvaluatesConditionsThatTheXmlFallbackCannot()
    {
        ProjectRef project = _tree.AddProject(
            "App/App.csproj",
            """
            <UserSecretsId Condition="'$(TargetFramework)' == 'net10.0'">conditional-id</UserSecretsId>
            <UserSecretsId Condition="'$(TargetFramework)' == 'net48'">wrong-id</UserSecretsId>
            """);

        SecretsIdResult result = await new UserSecretsIdResolver(_cli).ResolveAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(new SecretsIdResult("conditional-id", SecretsIdSource.MsBuild, FallbackUsed: false), result);
    }

    [Fact]
    public async Task Resolver_ReadsIdFromDirectoryBuildProps()
    {
        File.WriteAllText(Path.Combine(_tree.Root, "Directory.Build.props"), "<Project><PropertyGroup><UserSecretsId>shared-props-id</UserSecretsId></PropertyGroup></Project>");
        ProjectRef project = _tree.AddProject("App/App.fsproj");

        SecretsIdResult result = await new UserSecretsIdResolver(_cli).ResolveAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(new SecretsIdResult("shared-props-id", SecretsIdSource.MsBuild, FallbackUsed: false), result);
    }

    [Fact]
    public async Task Resolver_FallsBackToXml_WhenEvaluationFails()
    {
        string path = Path.Combine(_tree.Root, "Broken", "Broken.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            <Project Sdk="Not.A.Real.Sdk/1.0.0">
              <PropertyGroup><UserSecretsId>fallback-id</UserSecretsId></PropertyGroup>
            </Project>
            """);

        SecretsIdResult result = await new UserSecretsIdResolver(_cli).ResolveAsync(new ProjectRef(path, "Broken"), TestContext.Current.CancellationToken);

        Assert.Equal(new SecretsIdResult("fallback-id", SecretsIdSource.XmlFallback, FallbackUsed: true), result);
    }

    [Fact]
    public async Task Initializer_AddsAUserSecretsId_ThatTheResolverThenFinds()
    {
        ProjectRef project = _tree.AddProject("App/App.csproj");

        await new SecretsInitializer(_cli).InitAsync(project, TestContext.Current.CancellationToken);
        SecretsIdResult result = await new UserSecretsIdResolver(_cli).ResolveAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(SecretsIdSource.MsBuild, result.Source);
        Assert.False(string.IsNullOrWhiteSpace(result.Id));
        Assert.Contains($"<UserSecretsId>{result.Id}</UserSecretsId>", File.ReadAllText(project.Path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportService_EndToEnd_DiscoverAnalyzeInitAnalyzeApply()
    {
        ProjectRef project = _tree.AddProject("src/App/App.csproj");
        string secretsRoot = Path.Combine(_tree.Root, "usersecrets");
        ImportService service = new(
            new ProjectLocator(), new UserSecretsIdResolver(_cli), new SecretsInitializer(_cli), new JsonFlattener(), new ImportPlanner(),
            new SecretsStore(id => Path.Combine(secretsRoot, id, "secrets.json")), _cli);
        ImportRequest request = new(_tree.Root, new JsonInput("input.json", """{ "ConnectionStrings": { "Default": "Server=." }, "Port": 5432 }"""), ImportMode.Overwrite, null);
        CancellationToken ct = TestContext.Current.CancellationToken;

        ProjectInfo discovered = Assert.Single((await service.DiscoverProjectsAsync(_tree.Root, ct)).Projects);
        ImportAnalysis before = await service.AnalyzeAsync(request, ct);
        await service.InitAsync(before.Project.Project, ct);
        ImportAnalysis after = await service.AnalyzeAsync(request, ct);
        ImportResult result = await service.ApplyAsync(after, ct);

        Assert.Null(discovered.UserSecretsId);
        Assert.True(before.NeedsInit);
        Assert.True(before.CanInit);
        Assert.False(after.NeedsInit);
        Assert.Equal(SecretsIdSource.MsBuild, after.Project.Source);
        Assert.Equal(Path.Combine(secretsRoot, after.Project.UserSecretsId!, "secrets.json"), result.SecretsPath);
        Assert.Equal(2, result.Added);
        Assert.Contains("\"ConnectionStrings:Default\": \"Server=.\"", File.ReadAllText(result.SecretsPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportService_ScopeSolution_WarnsAboutAnIdSharedThroughDirectoryBuildProps()
    {
        File.WriteAllText(Path.Combine(_tree.Root, "Directory.Build.props"), "<Project><PropertyGroup><UserSecretsId>props-shared-id</UserSecretsId></PropertyGroup></Project>");
        ProjectRef api = _tree.AddProject("src/Api/Api.csproj");
        ProjectRef worker = _tree.AddProject("src/Worker/Worker.csproj");
        string slnx = Path.Combine(_tree.Root, "All.slnx");
        File.WriteAllText(slnx, """<Solution><Project Path="src/Api/Api.csproj" /><Project Path="src/Worker/Worker.csproj" /></Solution>""");
        string secretsRoot = Path.Combine(_tree.Root, "usersecrets");
        ImportService service = new(
            new ProjectLocator(), new UserSecretsIdResolver(_cli), new SecretsInitializer(_cli), new JsonFlattener(), new ImportPlanner(),
            new SecretsStore(id => Path.Combine(secretsRoot, id, "secrets.json")), _cli);
        ImportRequest request = new(api.Path, new JsonInput("input.json", """{ "Token": "do-not-print-me" }"""), ImportMode.Overwrite, null, slnx);

        ImportAnalysis analysis = await service.AnalyzeAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("props-shared-id", analysis.Project.UserSecretsId);
        Assert.Equal([worker], analysis.Project.SharedWith);
        ImportWarning warning = Assert.Single(analysis.Warnings);
        Assert.Equal(ImportWarningKind.SharedSecretsId, warning.Kind);
        Assert.Contains("Worker", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-print-me", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initializer_OnABrokenProject_IsIoFailure()
    {
        string path = Path.Combine(_tree.Root, "Broken", "Broken.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => new SecretsInitializer(_cli).InitAsync(new ProjectRef(path, "Broken"), TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
    }
}