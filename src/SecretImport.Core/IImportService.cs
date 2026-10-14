namespace SecretImport.Core;

public interface IImportService
{
    Task<IReadOnlyList<ProjectInfo>> DiscoverProjectsAsync(string path, CancellationToken ct = default);

    /// <summary>When the project has no UserSecretsId yet, returns NeedsInit = true and a plan compared against an empty store.</summary>
    Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default);

    /// <summary>Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.SdkUnavailable"/> when the .NET SDK is missing.</summary>
    Task InitAsync(ProjectRef project, CancellationToken ct = default);

    /// <summary>Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.ConcurrentModification"/> when secrets.json changed after Analyze.</summary>
    Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default);
}
