using System.Xml;
using System.Xml.Linq;

namespace SecretImport.Core;

/// <param name="Id">The UserSecretsId, or null when the project has none.</param>
/// <param name="Source">How the id was found; <see cref="SecretsIdSource.None"/> when <paramref name="Id"/> is null.</param>
/// <param name="FallbackUsed">True when MSBuild could not evaluate the project and the XML fallback was used.</param>
internal sealed record SecretsIdResult(string? Id, SecretsIdSource Source, bool FallbackUsed);

/// <summary>
/// Finds a project's UserSecretsId with <c>dotnet msbuild -getProperty:UserSecretsId</c>, falling back to reading
/// the project XML and the nearest <c>Directory.Build.props</c> when the SDK is missing or evaluation fails (ADR-0002).
/// </summary>
internal sealed class UserSecretsIdResolver(IDotnetCli dotnet)
{
    private const string PropertyName = "UserSecretsId";

    public async Task<SecretsIdResult> ResolveAsync(ProjectRef project, CancellationToken ct = default)
    {
        try
        {
            DotnetResult result = await dotnet.RunAsync(
                ["msbuild", project.Path, $"-getProperty:{PropertyName}", "-nologo"],
                Path.GetDirectoryName(project.Path),
                ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                string id = result.StandardOutput.Trim();
                return id.Length == 0
                    ? new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: false)
                    : new SecretsIdResult(id, SecretsIdSource.MsBuild, FallbackUsed: false);
            }
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.SdkUnavailable)
        {
            // Fall through to the XML fallback.
        }

        string? xmlId = ReadId(project.Path) ?? ReadId(FindNearestDirectoryBuildProps(project.Path));
        return xmlId is null
            ? new SecretsIdResult(null, SecretsIdSource.None, FallbackUsed: true)
            : new SecretsIdResult(xmlId, SecretsIdSource.XmlFallback, FallbackUsed: true);
    }

    /// <summary>The last non-empty, literal UserSecretsId in the file; conditions are ignored.</summary>
    private static string? ReadId(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return XDocument.Load(path).Descendants()
                .Where(static e => e.Name.LocalName == PropertyName)
                .Select(static e => e.Value.Trim())
                .LastOrDefault(static v => v.Length > 0 && !v.Contains("$(", StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindNearestDirectoryBuildProps(string projectPath)
    {
        for (DirectoryInfo? dir = new FileInfo(projectPath).Directory; dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "Directory.Build.props");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}