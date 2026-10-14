namespace SecretImport.Cli;

/// <summary>
/// Picks the scope Core uses to find projects that share the target's UserSecretsId (ADR-0013). This is CLI policy, not Core's:
/// <list type="bullet">
/// <item><c>--scope</c>, when given, is used as is.</item>
/// <item>A folder or <c>.sln</c>/<c>.slnx</c> passed to <c>--project</c> is its own scope.</item>
/// <item>For a project file, the nearest parent folder that contains a <c>.sln</c>/<c>.slnx</c> decides: exactly one solution there is the scope;
/// several solutions in that folder, or none in any parent, means no scope.</item>
/// </list>
/// </summary>
/// <param name="enumerateFiles">Lists the files directly in a folder. Defaults to the real file system; tests replace it.</param>
public sealed class ScopeResolver(Func<string, IEnumerable<string>>? enumerateFiles = null)
{
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    private readonly Func<string, IEnumerable<string>> enumerateFiles = enumerateFiles ?? Directory.EnumerateFiles;

    /// <returns>The scope path, or null for no scope (Core then skips the shared-id check).</returns>
    public string? Resolve(string project, string? explicitScope)
    {
        if (explicitScope is not null)
        {
            return explicitScope;
        }

        if (string.IsNullOrWhiteSpace(project))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(project);
        if (Directory.Exists(fullPath) || (File.Exists(fullPath) && HasExtension(fullPath, SolutionExtensions)))
        {
            return fullPath;
        }

        // A missing path or an unsupported file is reported by Core; there is nothing to scope.
        return File.Exists(fullPath) && HasExtension(fullPath, ProjectExtensions) ? FindNearestSolution(Path.GetDirectoryName(fullPath)) : null;
    }

    private string? FindNearestSolution(string? dir)
    {
        for (; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string[] solutions = ListSolutions(dir);
            if (solutions.Length > 0)
            {
                // Several solutions side by side (e.g. App.sln next to App.slnx) is ambiguous: don't guess.
                return solutions.Length == 1 ? solutions[0] : null;
            }
        }

        return null;
    }

    private string[] ListSolutions(string dir)
    {
        try
        {
            return [.. enumerateFiles(dir).Where(static f => HasExtension(f, SolutionExtensions))];
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // An unreadable folder can't decide anything; keep walking up.
            return [];
        }
    }

    private static bool HasExtension(string path, string[] extensions) =>
        extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
