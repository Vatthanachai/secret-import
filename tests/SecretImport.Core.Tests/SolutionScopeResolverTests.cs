namespace SecretImport.Core.Tests;

/// <summary>How the frontends pick the scope for the shared-id check when none is given explicitly (ADR-0013, Amendment 1).</summary>
public sealed class SolutionScopeResolverTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("secret-import-scope-").FullName;
    private readonly SolutionScopeResolver resolver;

    public SolutionScopeResolverTests()
    {
        // Only look inside the temp tree, so solutions that happen to sit above the temp folder can't leak into the results.
        resolver = new SolutionScopeResolver(dir => IsInsideRoot(dir) ? Directory.EnumerateFiles(dir) : []);
    }

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
    }

    private bool IsInsideRoot(string dir) =>
        Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar).StartsWith(root, StringComparison.OrdinalIgnoreCase);

    private string Touch(string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<x />");
        return path;
    }

    [Fact]
    public void ExplicitScope_IsUsedAsGiven()
    {
        string project = Touch("src/App/App.csproj");
        Touch("All.slnx");

        Assert.Equal("somewhere/Else.sln", resolver.Resolve(project, "somewhere/Else.sln"));
    }

    [Fact]
    public void FolderProject_IsItsOwnScope()
    {
        string folder = Path.GetDirectoryName(Touch("src/App/App.csproj"))!;

        Assert.Equal(folder, resolver.Resolve(folder, null));
    }

    [Theory]
    [InlineData("All.sln")]
    [InlineData("All.slnx")]
    [InlineData("All.SLNX")]
    public void SolutionProject_IsItsOwnScope(string file)
    {
        string solution = Touch(file);

        Assert.Equal(solution, resolver.Resolve(solution, null));
    }

    [Fact]
    public void RelativeFolder_IsMadeAbsolute()
    {
        string folder = Path.GetDirectoryName(Touch("src/App/App.csproj"))!;

        Assert.Equal(folder, resolver.Resolve(Path.GetRelativePath(Environment.CurrentDirectory, folder), null));
    }

    [Fact]
    public void ProjectFile_UsesASolutionInTheSameFolder()
    {
        string project = Touch("App/App.csproj");
        string solution = Touch("App/App.slnx");

        Assert.Equal(solution, resolver.Resolve(project, null));
    }

    [Fact]
    public void ProjectFile_UsesTheNearestSolutionAbove()
    {
        Touch("Root.sln");
        string nearest = Touch("src/Mid.slnx");
        string project = Touch("src/Deep/App/App.fsproj");

        Assert.Equal(nearest, resolver.Resolve(project, null));
    }

    [Fact]
    public void SlnNextToSlnx_InTheNearestFolder_UsesTheSlnx()
    {
        Touch("Root.slnx");
        Touch("src/A.sln");
        string slnx = Touch("src/A.slnx");
        string project = Touch("src/App/App.csproj");

        Assert.Equal(slnx, resolver.Resolve(project, null));
    }

    [Fact]
    public void SeveralSlnButOneSlnx_UsesTheSlnx()
    {
        Touch("App/A.sln");
        Touch("App/B.sln");
        string slnx = Touch("App/All.SLNX");
        string project = Touch("App/App.csproj");

        Assert.Equal(slnx, resolver.Resolve(project, null));
    }

    [Fact]
    public void SeveralSlnx_InTheNearestFolder_MeansNoScope_EvenWithOneFurtherUp()
    {
        Touch("Root.slnx");
        Touch("src/A.sln");
        Touch("src/A.slnx");
        Touch("src/B.slnx");
        string project = Touch("src/App/App.csproj");

        Assert.Null(resolver.Resolve(project, null));
    }

    [Fact]
    public void SeveralSlnOnly_InTheNearestFolder_MeansNoScope_EvenWithOneFurtherUp()
    {
        Touch("Root.sln");
        Touch("src/A.sln");
        Touch("src/B.sln");
        string project = Touch("src/App/App.csproj");

        Assert.Null(resolver.Resolve(project, null));
    }

    [Fact]
    public void ProjectFile_WithoutAnySolutionAbove_MeansNoScope()
    {
        Touch("README.md");
        Touch("src/App.sln.bak");
        string project = Touch("src/App/App.vbproj");

        Assert.Null(resolver.Resolve(project, null));
    }

    [Fact]
    public void UnreadableFolder_IsSkipped_AndTheWalkGoesOn()
    {
        string solution = Touch("All.slnx");
        string project = Touch("src/App/App.csproj");
        string blocked = Path.GetDirectoryName(project)!;
        SolutionScopeResolver withBlockedFolder = new(dir => string.Equals(dir, blocked, StringComparison.OrdinalIgnoreCase)
            ? throw new UnauthorizedAccessException("denied")
            : IsInsideRoot(dir) ? Directory.EnumerateFiles(dir) : []);

        Assert.Equal(solution, withBlockedFolder.Resolve(project, null));
    }

    [Fact]
    public void MissingProjectPath_MeansNoScope()
    {
        Touch("All.slnx");

        Assert.Null(resolver.Resolve(Path.Combine(root, "Gone", "Gone.csproj"), null));
    }

    [Fact]
    public void UnsupportedFile_MeansNoScope()
    {
        Touch("All.slnx");

        Assert.Null(resolver.Resolve(Touch("notes.txt"), null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankProject_MeansNoScope(string project)
    {
        Assert.Null(resolver.Resolve(project, null));
    }

    [Fact]
    public void DefaultConstructor_ReadsTheRealFileSystem()
    {
        string project = Touch("App/App.csproj");
        string solution = Touch("App/App.sln");

        Assert.Equal(solution, new SolutionScopeResolver().Resolve(project, null));
    }

    [Theory]
    [InlineData("App.csproj", true)]
    [InlineData("dir/App.FSPROJ", true)]
    [InlineData("App.vbproj", true)]
    [InlineData("All.slnx", false)]
    [InlineData("All.sln", false)]
    [InlineData("App.csproj.user", false)]
    [InlineData("src", false)]
    [InlineData("", false)]
    public void IsProjectFile_MatchesTheProjectExtensionsExactly(string path, bool expected)
    {
        Assert.Equal(expected, SolutionScopeResolver.IsProjectFile(path));
    }
}
