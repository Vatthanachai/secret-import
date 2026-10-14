namespace SecretImport.Cli.Tests;

/// <summary>How the CLI picks the scope for the shared-id check when <c>--scope</c> is omitted (ADR-0013).</summary>
public sealed class ScopeResolverTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("secret-import-scope-").FullName;
    private readonly ScopeResolver resolver;

    public ScopeResolverTests()
    {
        // Only look inside the temp tree, so solutions that happen to sit above the temp folder can't leak into the results.
        resolver = new ScopeResolver(dir => IsInsideRoot(dir) ? Directory.EnumerateFiles(dir) : []);
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
    public void SeveralSolutionsInTheNearestFolder_MeansNoScope_EvenWithOneFurtherUp()
    {
        Touch("Root.slnx");
        Touch("src/A.sln");
        Touch("src/A.slnx");
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
        ScopeResolver withBlockedFolder = new(dir => string.Equals(dir, blocked, StringComparison.OrdinalIgnoreCase)
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

        Assert.Equal(solution, new ScopeResolver().Resolve(project, null));
    }
}
