using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary>Hand-written <see cref="IImportService"/> double that records calls and returns canned results.</summary>
internal sealed class FakeImportService : IImportService
{
    private readonly Queue<ImportAnalysis> analyses = new();

    public List<ImportRequest> AnalyzeRequests { get; } = [];

    public List<ProjectRef> InitCalls { get; } = [];

    public List<ImportAnalysis> ApplyCalls { get; } = [];

    public Exception? AnalyzeException { get; set; }

    public Exception? InitException { get; set; }

    public Exception? ApplyException { get; set; }

    public ImportResult ApplyResult { get; set; } = new(1, 2, 3, "/secrets/secrets.json", "/secrets/secrets.json.bak");

    public List<ExportRequest> ExportRequests { get; } = [];

    public List<WriteExportCall> WriteExportCalls { get; } = [];

    public Exception? ExportException { get; set; }

    public Exception? WriteExportException { get; set; }

    public ExportResult ExportResult { get; set; } = TestData.Export();

    /// <summary>Analyses returned by successive <see cref="AnalyzeAsync"/> calls; the last one repeats.</summary>
    public FakeImportService Returns(params ImportAnalysis[] results)
    {
        foreach (ImportAnalysis result in results)
        {
            analyses.Enqueue(result);
        }

        return this;
    }

    public Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default)
    {
        throw new NotSupportedException("The CLI never discovers projects; Analyze resolves --project.");
    }

    public Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default)
    {
        AnalyzeRequests.Add(request);
        if (AnalyzeException is not null)
        {
            throw AnalyzeException;
        }

        ImportAnalysis result = analyses.Count > 1 ? analyses.Dequeue() : analyses.Peek();
        return Task.FromResult(result with { Request = request });
    }

    public Task InitAsync(ProjectRef project, CancellationToken ct = default)
    {
        InitCalls.Add(project);
        return InitException is null ? Task.CompletedTask : Task.FromException(InitException);
    }

    public Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default)
    {
        ApplyCalls.Add(analysis);
        return ApplyException is null ? Task.FromResult(ApplyResult) : Task.FromException<ImportResult>(ApplyException);
    }

    public Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        ExportRequests.Add(request);
        return ExportException is null ? Task.FromResult(ExportResult) : Task.FromException<ExportResult>(ExportException);
    }

    public Task WriteExportAsync(string path, string json, bool overwrite, CancellationToken ct = default)
    {
        WriteExportCalls.Add(new WriteExportCall(path, json, overwrite));
        return WriteExportException is null ? Task.CompletedTask : Task.FromException(WriteExportException);
    }
}

internal sealed record WriteExportCall(string Path, string Json, bool Overwrite);
