using SecretImport.Core;

namespace SecretImport.Desktop.Tests.Fakes;

/// <summary>Hand-written <see cref="IImportService"/> fake. Each operation is a delegate; calls are recorded.</summary>
internal sealed class FakeImportService : IImportService
{
    public Func<string, IReadOnlyList<ProjectInfo>> Discover { get; set; } = static _ => [];

    /// <summary>The <see cref="DiscoveryResult.Warnings"/> returned with every discovery.</summary>
    public IReadOnlyList<ImportWarning> DiscoverWarnings { get; set; } = [];

    public Func<ImportRequest, ImportAnalysis> Analyze { get; set; } = static r => TestData.Analysis(r);

    public Action<ProjectRef> Init { get; set; } = static _ => { };

    public Func<ImportAnalysis, ImportResult> Apply { get; set; } = static a => new ImportResult(1, 0, 0, a.SecretsPath ?? "secrets.json", "secrets.json.bak");

    /// <summary>When set, every call waits for this task first (to observe the busy state).</summary>
    public Task? Gate { get; set; }

    public List<string> DiscoverCalls { get; } = [];

    public List<ImportRequest> AnalyzeCalls { get; } = [];

    public List<ProjectRef> InitCalls { get; } = [];

    public List<ImportAnalysis> ApplyCalls { get; } = [];

    public async Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default)
    {
        await WaitGateAsync();
        DiscoverCalls.Add(path);
        return new DiscoveryResult(Discover(path), DiscoverWarnings);
    }

    public async Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default)
    {
        await WaitGateAsync();
        AnalyzeCalls.Add(request);
        return Analyze(request);
    }

    public async Task InitAsync(ProjectRef project, CancellationToken ct = default)
    {
        await WaitGateAsync();
        InitCalls.Add(project);
        Init(project);
    }

    public async Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default)
    {
        await WaitGateAsync();
        ApplyCalls.Add(analysis);
        return Apply(analysis);
    }

    private Task WaitGateAsync()
    {
        return Gate ?? Task.CompletedTask;
    }
}
