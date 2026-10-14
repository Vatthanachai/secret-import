namespace SecretImport.Core.Tests;

public sealed class UserSecretsIdResolverTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("secret-import-resolver-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string Write(string relative, string content)
    {
        string path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private ProjectRef Project(string content = "<Project Sdk=\"Microsoft.NET.Sdk\" />", string relative = "src/App/App.csproj")
    {
        string path = Write(relative, content);
        return new ProjectRef(path, Path.GetFileNameWithoutExtension(path));
    }

    private static Task<SecretsIdResult> Resolve(IDotnetCli cli, ProjectRef project) =>
        new UserSecretsIdResolver(cli).ResolveAsync(project, TestContext.Current.CancellationToken);

    [Fact]
    public async Task MsBuild_ReturnsTrimmedId_AndRunsGetPropertyInTheProjectFolder()
    {
        FakeDotnetCli cli = new() { Handler = static _ => new DotnetResult(0, "  my-id \r\n", string.Empty) };
        ProjectRef project = Project();

        SecretsIdResult result = await Resolve(cli, project);

        Assert.Equal(new SecretsIdResult("my-id", SecretsIdSource.MsBuild, FallbackUsed: false), result);
        (IReadOnlyList<string> args, string? cwd) = Assert.Single(cli.Calls);
        Assert.Equal(["msbuild", project.Path, "-getProperty:UserSecretsId", "-nologo"], args);
        Assert.Equal(Path.GetDirectoryName(project.Path), cwd);
    }

    [Fact]
    public async Task MsBuild_WithEmptyOutput_MeansNoId()
    {
        FakeDotnetCli cli = new() { Handler = static _ => new DotnetResult(0, "\r\n", string.Empty) };

        SecretsIdResult result = await Resolve(cli, Project("<Project><PropertyGroup><UserSecretsId>xml-id</UserSecretsId></PropertyGroup></Project>"));

        Assert.Equal(new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: false), result);
    }

    [Fact]
    public async Task MsBuildFailure_FallsBackToTheProjectXml()
    {
        FakeDotnetCli cli = new() { Handler = static _ => new DotnetResult(1, string.Empty, "error MSB4236: SDK not found") };

        SecretsIdResult result = await Resolve(cli, Project("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <UserSecretsId> xml-id </UserSecretsId>
              </PropertyGroup>
            </Project>
            """));

        Assert.Equal(new SecretsIdResult("xml-id", SecretsIdSource.XmlFallback, FallbackUsed: true), result);
    }

    [Fact]
    public async Task SdkUnavailable_FallsBackToTheProjectXml_WithMsBuildNamespace()
    {
        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project("""
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><UserSecretsId>ns-id</UserSecretsId></PropertyGroup>
            </Project>
            """));

        Assert.Equal(new SecretsIdResult("ns-id", SecretsIdSource.XmlFallback, FallbackUsed: true), result);
    }

    [Fact]
    public async Task XmlFallback_LastDefinitionWins()
    {
        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project("""
            <Project>
              <PropertyGroup><UserSecretsId>first</UserSecretsId></PropertyGroup>
              <PropertyGroup Condition="'$(Configuration)' == 'Debug'"><UserSecretsId>second</UserSecretsId></PropertyGroup>
              <PropertyGroup><UserSecretsId></UserSecretsId></PropertyGroup>
            </Project>
            """));

        Assert.Equal("second", result.Id);
    }

    [Fact]
    public async Task XmlFallback_IgnoresPropertyReferences()
    {
        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project("<Project><PropertyGroup><UserSecretsId>$(MyId)</UserSecretsId></PropertyGroup></Project>"));

        Assert.Equal(new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: true), result);
    }

    [Fact]
    public async Task XmlFallback_UsesTheNearestDirectoryBuildProps_WhenTheProjectHasNoId()
    {
        Write("Directory.Build.props", "<Project><PropertyGroup><UserSecretsId>far</UserSecretsId></PropertyGroup></Project>");
        Write("src/Directory.Build.props", "<Project><PropertyGroup><UserSecretsId>near</UserSecretsId></PropertyGroup></Project>");

        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project());

        Assert.Equal(new SecretsIdResult("near", SecretsIdSource.XmlFallback, FallbackUsed: true), result);
    }

    [Fact]
    public async Task XmlFallback_ProjectIdOverridesDirectoryBuildProps()
    {
        Write("src/App/Directory.Build.props", "<Project><PropertyGroup><UserSecretsId>props</UserSecretsId></PropertyGroup></Project>");

        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project("<Project><PropertyGroup><UserSecretsId>proj</UserSecretsId></PropertyGroup></Project>"));

        Assert.Equal("proj", result.Id);
    }

    [Fact]
    public async Task XmlFallback_OnlyTheNearestDirectoryBuildPropsCounts()
    {
        // MSBuild imports only the nearest Directory.Build.props unless it imports its parent itself.
        Write("Directory.Build.props", "<Project><PropertyGroup><UserSecretsId>far</UserSecretsId></PropertyGroup></Project>");
        Write("src/Directory.Build.props", "<Project />");

        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project());

        Assert.Null(result.Id);
    }

    [Fact]
    public async Task XmlFallback_NoIdAnywhere_IsNone()
    {
        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project());

        Assert.Equal(new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: true), result);
    }

    [Fact]
    public async Task XmlFallback_MalformedXml_IsTreatedAsNoId()
    {
        Write("src/Directory.Build.props", "<Project><PropertyGroup><UserSecretsId>props");

        SecretsIdResult result = await Resolve(FakeDotnetCli.Unavailable(), Project("<Project><Unclosed>"));

        Assert.Null(result.Id);
    }
}