using System.Text.Json;

namespace SecretImport.Desktop.Services;

/// <summary>
/// <see cref="IRecentProjectsStore"/> backed by a small JSON array of paths, by default under
/// <see cref="Environment.SpecialFolder.ApplicationData"/>. The list is a convenience: read and write failures are
/// swallowed and never block an import.
/// </summary>
public sealed class JsonRecentProjectsStore(string filePath) : IRecentProjectsStore
{
    public const int MaxEntries = 10;

    public static string DefaultFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecretImport", "recent-projects.json");

    public string FilePath { get; } = filePath;

    public IReadOnlyList<string> Load()
    {
        try
        {
            string[] paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath)) ?? [];
            return Normalize(paths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public IReadOnlyList<string> Add(string projectPath)
    {
        string[] paths = Normalize([projectPath, .. Load()]);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(paths));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a recent project must not fail the wizard.
        }

        return paths;
    }

    private static string[] Normalize(IEnumerable<string?> paths)
    {
        return paths
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxEntries)
            .ToArray();
    }
}
