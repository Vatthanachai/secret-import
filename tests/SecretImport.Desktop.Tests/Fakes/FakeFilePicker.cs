using SecretImport.Desktop.Services;

namespace SecretImport.Desktop.Tests.Fakes;

internal sealed class FakeFilePicker : IFilePickerService
{
    public string? ProjectFile { get; set; }

    public string? Folder { get; set; }

    public string? JsonFile { get; set; }

    public Task<string?> PickProjectFileAsync()
    {
        return Task.FromResult(ProjectFile);
    }

    public Task<string?> PickFolderAsync()
    {
        return Task.FromResult(Folder);
    }

    public Task<string?> PickJsonFileAsync()
    {
        return Task.FromResult(JsonFile);
    }
}

internal sealed class FakeRecentProjectsStore(params string[] initial) : IRecentProjectsStore
{
    public List<string> Paths { get; } = [.. initial];

    public IReadOnlyList<string> Load()
    {
        return Paths.ToArray();
    }

    public IReadOnlyList<string> Add(string projectPath)
    {
        Paths.Remove(projectPath);
        Paths.Insert(0, projectPath);
        return Paths.ToArray();
    }
}

/// <summary>A temporary directory deleted on dispose.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("secret-import-desktop-tests-").FullName;

    public string File(string name, string content)
    {
        string path = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
