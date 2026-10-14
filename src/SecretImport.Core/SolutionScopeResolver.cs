namespace SecretImport.Core;

/// <summary>
/// Picks the scope used to find projects that share the target's UserSecretsId (ADR-0013, Amendment 1).
/// Lives in Core so the CLI and the desktop app apply the same rule:
/// <list type="bullet">
/// <item>An explicit scope, when given, is used as is.</item>
/// <item>A folder or <c>.sln</c>/<c>.slnx</c> project path is its own scope.</item>
/// <item>For a project file, the nearest parent folder that contains a <c>.sln</c>/<c>.slnx</c> decides: exactly one solution there is the scope;
/// several solutions with exactly one <c>.slnx</c> among them means that <c>.slnx</c>; anything else, or no solution in any parent, means no scope.</item>
/// </list>
/// </summary>
/// <param name="enumerateFiles">Lists the files directly in a folder. Defaults to the real file system; tests replace it.</param>
public sealed class SolutionScopeResolver(Func<string, IEnumerable<string>>? enumerateFiles = null)
{
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    private readonly Func<string, IEnumerable<string>> enumerateFiles = enumerateFiles ?? Directory.EnumerateFiles;

    /// <summary>Whether <paramref name="path"/> names a <c>.csproj</c>/<c>.fsproj</c>/<c>.vbproj</c> (by extension only; the file system isn't read).</summary>
    public static bool IsProjectFile(string path) => HasExtension(path, ProjectExtensions);

    /// <param name="projectPath">The target: a project file, a folder, or a solution. A relative path is made absolute.</param>
    /// <param name="explicitScope">A scope the user chose; returned unchanged when not null.</param>
    /// <returns>The scope path, or null for no scope (Core then skips the shared-id check).</returns>
    public string? Resolve(string projectPath, string? explicitScope)
    {
        if (explicitScope is not null)
        {
            return explicitScope;
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(projectPath);
        if (Directory.Exists(fullPath) || (File.Exists(fullPath) && HasExtension(fullPath, SolutionExtensions)))
        {
            return fullPath;
        }

        // A missing path or an unsupported file is reported by Core; there is nothing to scope.
        return File.Exists(fullPath) && IsProjectFile(fullPath) ? FindNearestSolution(Path.GetDirectoryName(fullPath)) : null;
    }

    private string? FindNearestSolution(string? dir)
    {
        for (; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            string[] solutions = ListSolutions(dir);
            if (solutions.Length > 0)
            {
                return PickSolution(solutions);
            }
        }

        return null;
    }

    /// <summary>One solution wins; among several, a single <c>.slnx</c> wins (App.sln next to App.slnx → App.slnx); otherwise it's ambiguous.</summary>
    private static string? PickSolution(string[] solutions)
    {
        if (solutions.Length == 1)
        {
            return solutions[0];
        }

        string[] slnx = [.. solutions.Where(static f => string.Equals(Path.GetExtension(f), ".slnx", StringComparison.OrdinalIgnoreCase))];
        return slnx.Length == 1 ? slnx[0] : null;
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
