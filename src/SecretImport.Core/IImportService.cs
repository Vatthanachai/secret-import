namespace SecretImport.Core;

public interface IImportService
{
    Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// When the project has no UserSecretsId yet, returns NeedsInit = true and a plan compared against an empty store.
    /// With <see cref="ImportRequest.ScopePath"/> set, fills <see cref="ProjectInfo.SharedWith"/> from the projects in that scope
    /// and warns when the id is shared; a scope that does not exist throws <see cref="ImportException"/> with <see cref="ImportErrorKind.InvalidInput"/>.
    /// </summary>
    Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default);

    /// <summary>Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.SdkUnavailable"/> when the .NET SDK is missing.</summary>
    Task InitAsync(ProjectRef project, CancellationToken ct = default);

    /// <summary>Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.ConcurrentModification"/> when secrets.json changed after Analyze.</summary>
    Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default);
}
