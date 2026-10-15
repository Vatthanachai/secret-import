using SecretImport.Desktop.Services;
using SecretImport.Desktop.Tests.Fakes;

namespace SecretImport.Desktop.Tests;

public sealed class JsonRecentProjectsStoreTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void DefaultFilePath_IsUnderApplicationData()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.Equal(Path.Combine(appData, "SecretImport", "recent-projects.json"), JsonRecentProjectsStore.DefaultFilePath);
    }

    [Fact]
    public void Load_WithoutAFile_IsEmpty()
    {
        JsonRecentProjectsStore store = new(Path.Combine(_temp.Path, "missing", "recent.json"));

        Assert.Empty(store.Load());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Load_WithAnUnreadableFile_IsEmpty(string content)
    {
        JsonRecentProjectsStore store = new(_temp.File("recent.json", content));

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Load_DropsBlankAndDuplicateEntries()
    {
        JsonRecentProjectsStore store = new(_temp.File("recent.json", """["a", "", null, "b", "a"]"""));

        Assert.Equal(["a", "b"], store.Load());
    }

    [Fact]
    public void Add_CreatesTheFolderAndPutsTheNewestFirst()
    {
        string file = Path.Combine(_temp.Path, "nested", "recent.json");
        JsonRecentProjectsStore store = new(file);

        store.Add("a.csproj");
        store.Add("b.csproj");
        IReadOnlyList<string> result = store.Add("a.csproj");

        Assert.Equal(file, store.FilePath);
        Assert.Equal(["a.csproj", "b.csproj"], result);
        Assert.Equal(["a.csproj", "b.csproj"], new JsonRecentProjectsStore(file).Load());
        Assert.Equal("""["a.csproj","b.csproj"]""", File.ReadAllText(file));
    }

    [Fact]
    public void Add_KeepsAtMostTheMaximum()
    {
        JsonRecentProjectsStore store = new(Path.Combine(_temp.Path, "recent.json"));

        for (int i = 0; i < JsonRecentProjectsStore.MaxEntries + 5; i++)
        {
            store.Add($"p{i}.csproj");
        }

        IReadOnlyList<string> paths = store.Load();
        Assert.Equal(JsonRecentProjectsStore.MaxEntries, paths.Count);
        Assert.Equal($"p{JsonRecentProjectsStore.MaxEntries + 4}.csproj", paths[0]);
    }

    [Fact]
    public void Add_WhenTheFileCannotBeWritten_StillReturnsTheList()
    {
        // A directory where the file should be makes both the read and the write fail with an I/O error.
        string blocked = Path.Combine(_temp.Path, "blocked");
        Directory.CreateDirectory(blocked);
        JsonRecentProjectsStore store = new(blocked);

        Assert.Equal(["a.csproj"], store.Add("a.csproj"));
        Assert.Empty(store.Load());
    }
}
