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

    /// <summary>
    /// Reads a project's secrets and renders them as JSON (ADR-0015). Writes nothing.
    /// Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.InvalidInput"/> when the project has no UserSecretsId,
    /// the section has no values, or a key cannot be nested (use <see cref="ExportRequest.Flat"/>).
    /// A project with an id but no secrets.json exports <c>{}</c>.
    /// </summary>
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Writes <paramref name="json"/> to <paramref name="path"/> atomically as UTF-8 without a BOM; no <c>.bak</c>.
    /// On Unix a new file gets mode 600 and an overwritten file keeps its mode (ADR-0009).
    /// Throws <see cref="ImportException"/> with <see cref="ImportErrorKind.InvalidInput"/> when the file exists and
    /// <paramref name="overwrite"/> is false (the file is not touched), or <see cref="ImportErrorKind.IoFailure"/> when it cannot be written.
    /// </summary>
    Task WriteExportAsync(string path, string json, bool overwrite, CancellationToken ct = default);
}
