using SecretImport.Core;

namespace SecretImport.Desktop.Tests.Fakes;

internal static class TestData
{
    public static ProjectInfo Project(string name, string? secretsId = "id-1", params ProjectRef[] sharedWith)
    {
        return new ProjectInfo(
            new ProjectRef(Path.Combine("src", name, name + ".csproj"), name),
            secretsId,
            secretsId is null ? SecretsIdSource.None : SecretsIdSource.MsBuild,
            sharedWith);
    }

    public static IReadOnlyList<SecretChange> DefaultChanges { get; } =
    [
        new("Db:Password", SecretChangeKind.Changed, "old-secret", "new-secret"),
        new("Api:Key", SecretChangeKind.Added, null, "api-secret"),
    ];

    public static ImportAnalysis Analysis(
        ImportRequest request,
        bool needsInit = false,
        bool canInit = true,
        IReadOnlyList<SecretChange>? changes = null,
        IReadOnlyList<ImportWarning>? warnings = null)
    {
        IReadOnlyList<SecretChange> planChanges = changes ?? DefaultChanges;
        Dictionary<string, string> result = planChanges
            .Where(static c => c.NewValue is not null)
            .ToDictionary(static c => c.Key, static c => c.NewValue!);
        ProjectInfo project = new(
            new ProjectRef(request.ProjectPath, Path.GetFileNameWithoutExtension(request.ProjectPath)),
            needsInit ? null : "id-1",
            needsInit ? SecretsIdSource.None : SecretsIdSource.MsBuild,
            []);
        return new ImportAnalysis(
            project, request, needsInit ? null : Path.Combine("secrets", "id-1", "secrets.json"),
            needsInit, canInit, ExistingCount: 1, IncomingCount: planChanges.Count,
            new ImportPlan(planChanges, result), warnings ?? [], ExistingFileHash: null);
    }
}
