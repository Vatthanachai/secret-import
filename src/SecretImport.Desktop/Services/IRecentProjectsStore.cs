namespace SecretImport.Desktop.Services;

/// <summary>Remembers recently used project paths. Stores paths only, never secret values.</summary>
public interface IRecentProjectsStore
{
    /// <summary>The recent project paths, most recent first.</summary>
    IReadOnlyList<string> Load();

    /// <summary>Moves <paramref name="projectPath"/> to the top of the list, saves it and returns the new list.</summary>
    IReadOnlyList<string> Add(string projectPath);
}
