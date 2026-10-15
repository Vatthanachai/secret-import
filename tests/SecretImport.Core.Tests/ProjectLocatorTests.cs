namespace SecretImport.Core.Tests;

public sealed class ProjectLocatorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("secret-import-locator-").FullName;
    private readonly ProjectLocator _locator = new();

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string Touch(string relative, string content = "<Project Sdk=\"Microsoft.NET.Sdk\" />")
    {
        string path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<ProjectLocation> Locate(string path) => _locator.LocateAsync(path, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("App.csproj", "App")]
    [InlineData("Lib.fsproj", "Lib")]
    [InlineData("Legacy.VB.vbproj", "Legacy.VB")]
    [InlineData("Upper.CSPROJ", "Upper")]
    public async Task ProjectFile_ReturnsThatProject(string file, string name)
    {
        string path = Touch(file);

        ProjectLocation location = await Locate(path);

        ProjectRef project = Assert.Single(location.Projects);
        Assert.Equal(new ProjectRef(path, name), project);
        Assert.Empty(location.Warnings);
    }

    [Fact]
    public async Task RelativePath_IsMadeAbsolute()
    {
        string path = Touch("App.csproj");

        ProjectLocation location = await Locate(Path.GetRelativePath(Environment.CurrentDirectory, path));

        Assert.Equal(path, Assert.Single(location.Projects).Path);
    }

    [Fact]
    public async Task Folder_FindsAllProjectKindsRecursively_SortedByPath_SkippingBuildAndToolFolders()
    {
        string b = Touch("src/B/B.fsproj");
        string a = Touch("src/A/A.csproj");
        string c = Touch("C.vbproj");
        Touch("src/A/bin/Debug/Copy.csproj");
        Touch("src/A/obj/Gen.csproj");
        Touch("OBJ/Upper.csproj");
        Touch(".git/x/Git.csproj");
        Touch("node_modules/pkg/Node.csproj");
        Touch("db/Db.sqlproj");
        Touch("README.md", "text");

        ProjectLocation location = await Locate(_dir);

        Assert.Equal([c, a, b], location.Projects.Select(static p => p.Path));
    }

    [Fact]
    public async Task EmptyFolder_IsNoProjectFound()
    {
        Touch("notes.txt", "x");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(_dir));

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
        Assert.Contains(_dir, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingPath_IsNoProjectFound()
    {
        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(Path.Combine(_dir, "nope.csproj")));

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankPath_IsInvalidInput(string path)
    {
        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(path));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public async Task UnsupportedFile_IsInvalidInput()
    {
        string path = Touch("Db.sqlproj");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(path));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public async Task Slnx_ReturnsSupportedProjects_SkipsOtherTypes_AndWarnsAboutMissingFiles()
    {
        string a = Touch("src/A/A.csproj");
        string b = Touch("src/B/B.fsproj");
        Touch("db/Db.sqlproj");
        string sln = Touch("All.slnx", """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/B/B.fsproj" />
                <Project Path="src\A\A.csproj" />
              </Folder>
              <Project Path="db/Db.sqlproj" />
              <Project Path="gone/Gone.csproj" />
            </Solution>
            """);

        ProjectLocation location = await Locate(sln);

        Assert.Equal([a, b], location.Projects.Select(static p => p.Path));
        ImportWarning warning = Assert.Single(location.Warnings);
        Assert.Equal(ImportWarningKind.MissingProjectFile, warning.Kind);
        Assert.Contains("Gone.csproj", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sln_ReturnsItsProjects()
    {
        string a = Touch("A/A.csproj");
        string sln = Touch("Classic.sln", """

            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "A", "A\A.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        ProjectLocation location = await Locate(sln);

        Assert.Equal([a], location.Projects.Select(static p => p.Path));
    }

    [Fact]
    public async Task SolutionWithOnlyMissingProjects_IsNoProjectFound_AndKeepsTheWarning()
    {
        string sln = Touch("Empty.slnx", """<Solution><Project Path="gone/Gone.csproj" /></Solution>""");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(sln));

        Assert.Equal(ImportErrorKind.NoProjectFound, ex.Kind);
        Assert.Contains("Gone.csproj", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowEmpty_EmptyFolder_ReturnsNoProjects()
    {
        ProjectLocation location = await _locator.LocateAsync(_dir, allowEmpty: true, TestContext.Current.CancellationToken);

        Assert.Empty(location.Projects);
        Assert.Empty(location.Warnings);
    }

    [Fact]
    public async Task AllowEmpty_SolutionWithOnlyMissingProjects_ReturnsTheWarning()
    {
        string sln = Touch("Empty.slnx", """<Solution><Project Path="gone/Gone.csproj" /></Solution>""");

        ProjectLocation location = await _locator.LocateAsync(sln, allowEmpty: true, TestContext.Current.CancellationToken);

        Assert.Empty(location.Projects);
        Assert.Equal(ImportWarningKind.MissingProjectFile, Assert.Single(location.Warnings).Kind);
    }

    [Fact]
    public async Task CorruptSolution_IsInvalidInput()
    {
        string sln = Touch("Broken.slnx", "<Solution><Project");

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Locate(sln));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("Broken.slnx", ex.Message, StringComparison.Ordinal);
    }
}