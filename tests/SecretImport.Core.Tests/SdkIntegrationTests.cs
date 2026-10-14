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
    public async Task Initializer_OnABrokenProject_IsIoFailure()
    {
        string path = Path.Combine(_tree.Root, "Broken", "Broken.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => new SecretsInitializer(_cli).InitAsync(new ProjectRef(path, "Broken"), TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
    }
}