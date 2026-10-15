using SecretImport.Core;

namespace SecretImport.Cli.Tests;

internal static class TestData
{
    public const string SecretValue = "s3cr3t-NEW-value";
    public const string OldSecretValue = "s3cr3t-OLD-value";

    public static readonly ProjectRef Project = new("/repo/src/App/App.csproj", "App");

    public static readonly ImportRequest PlaceholderRequest = new(Project.Path, new JsonInput("placeholder", "{}"), ImportMode.Overwrite, null);

    /// <summary>Exported JSON as Core renders it: LF line endings, a trailing newline, and text that would break Spectre markup.</summary>
    public const string ExportJson = "{\n  \"Api\": {\n    \"Key\": \"" + SecretValue + "\"\n  },\n  \"Markup\": \"[red]x[/] ค่า <&>\"\n}\n";

    public static ExportResult Export(string json = ExportJson, int keyCount = 2, params ImportWarning[] warnings) =>
        new(new ProjectInfo(Project, "app-secrets-id", SecretsIdSource.MsBuild, []), "/secrets/app-secrets-id/secrets.json", keyCount, json, warnings);

    public static SecretChange[] SomeChanges() =>
    [
        new("Api:Key", SecretChangeKind.Added, null, SecretValue),
        new("Db:Password", SecretChangeKind.Changed, OldSecretValue, SecretValue),
        new("Same", SecretChangeKind.Unchanged, OldSecretValue, OldSecretValue),
    ];

    public static ImportAnalysis Initialized(params SecretChange[] changes) => Analysis(changes, needsInit: false);

    public static ImportAnalysis NotInitialized(bool canInit = true, params SecretChange[] changes) => Analysis(changes, needsInit: true, canInit: canInit);

    public static ImportAnalysis Analysis(
        IReadOnlyList<SecretChange> changes,
        bool needsInit = false,
        bool canInit = true,
        IReadOnlyList<ImportWarning>? warnings = null,
        IReadOnlyList<ProjectRef>? sharedWith = null)
    {
        ProjectInfo info = new(Project, needsInit ? null : "app-secrets-id", needsInit ? SecretsIdSource.None : SecretsIdSource.MsBuild, sharedWith ?? []);
        Dictionary<string, string> result = changes
            .Where(static c => c.NewValue is not null && c.Kind != SecretChangeKind.Removed)
            .ToDictionary(static c => c.Key, static c => c.NewValue!);
        return new ImportAnalysis(
            info, PlaceholderRequest, needsInit ? null : "/secrets/app-secrets-id/secrets.json",
            needsInit, canInit, ExistingCount: 2, IncomingCount: changes.Count,
            new ImportPlan(changes, result), warnings ?? [], ExistingFileHash: needsInit ? null : "hash");
    }
}
