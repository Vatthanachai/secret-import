using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace SecretImport.Core;

internal sealed record ProjectLocation(IReadOnlyList<ProjectRef> Projects, IReadOnlyList<ImportWarning> Warnings);

/// <summary>
/// Turns a user-supplied path (project file, folder, or <c>.sln</c>/<c>.slnx</c>) into project files (ADR-0006).
/// Solutions are read with SolutionPersistence, so no SDK is needed.
/// </summary>
internal sealed class ProjectLocator
{
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] SkippedFolders = ["bin", "obj", ".git", "node_modules"];

    /// <exception cref="ImportException">
    /// <see cref="ImportErrorKind.InvalidInput"/> for a blank path, an unsupported file or an unreadable solution;
    /// <see cref="ImportErrorKind.NoProjectFound"/> when the path does not exist or contains no project.
    /// </exception>
    public Task<ProjectLocation> LocateAsync(string path, CancellationToken ct = default) => LocateAsync(path, allowEmpty: false, ct);

    /// <param name="allowEmpty">True returns an empty list (with any warnings) instead of throwing when no project is found.</param>
    public async Task<ProjectLocation> LocateAsync(string path, bool allowEmpty, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ImportException(ImportErrorKind.InvalidInput, "No project path was given.");
        }

        string fullPath = Path.GetFullPath(path);
        List<ImportWarning> warnings = [];
        List<string> files;
        if (Directory.Exists(fullPath))
        {
            files = [.. FindProjects(fullPath)];
        }
        else if (!File.Exists(fullPath))
        {
            throw new ImportException(ImportErrorKind.NoProjectFound, $"Path not found: {fullPath}");
        }
        else if (HasExtension(fullPath, ProjectExtensions))
        {
            files = [fullPath];
        }
        else if (HasExtension(fullPath, SolutionExtensions))
        {
            files = await ReadSolutionAsync(fullPath, warnings, ct).ConfigureAwait(false);
        }
        else
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"{fullPath} is not a .csproj, .fsproj, .vbproj, .sln or .slnx file.");
        }

        if (files.Count == 0 && !allowEmpty)
        {
            string details = string.Concat(warnings.Select(static w => " " + w.Message));
            throw new ImportException(ImportErrorKind.NoProjectFound, $"No .csproj, .fsproj or .vbproj project was found in {fullPath}.{details}");
        }

        ProjectRef[] projects = [.. files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(static f => new ProjectRef(f, Path.GetFileNameWithoutExtension(f)))];
        return new ProjectLocation(projects, warnings);
    }

    private static IEnumerable<string> FindProjects(string root)
    {
        EnumerationOptions options = new() { IgnoreInaccessible = true, RecurseSubdirectories = false };
        Stack<string> pending = new([root]);
        while (pending.Count > 0)
        {
            string dir = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(dir, "*", options))
            {
                if (HasExtension(file, ProjectExtensions))
                {
                    yield return file;
                }
            }

            foreach (string sub in Directory.EnumerateDirectories(dir, "*", options))
            {
                if (!SkippedFolders.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(sub);
                }
            }
        }
    }

    private static async Task<List<string>> ReadSolutionAsync(string solutionPath, List<ImportWarning> warnings, CancellationToken ct)
    {
        ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath)!;
        SolutionModel solution;
        try
        {
            solution = await serializer.OpenAsync(solutionPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SolutionException or System.Xml.XmlException or FormatException or IOException or UnauthorizedAccessException)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"Cannot read solution {solutionPath}: {ex.Message}", innerException: ex);
        }

        string solutionDir = Path.GetDirectoryName(solutionPath)!;
        List<string> files = [];
        foreach (SolutionProjectModel project in solution.SolutionProjects)
        {
            string relative = project.FilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string projectPath = Path.GetFullPath(Path.Combine(solutionDir, relative));
            if (!HasExtension(projectPath, ProjectExtensions))
            {
                continue;
            }

            if (File.Exists(projectPath))
            {
                files.Add(projectPath);
            }
            else
            {
                warnings.Add(new ImportWarning(ImportWarningKind.MissingProjectFile, $"Project '{project.FilePath}' is listed in {Path.GetFileName(solutionPath)} but {projectPath} does not exist; it was skipped."));
            }
        }

        return files;
    }

    private static bool HasExtension(string path, string[] extensions) =>
        extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}