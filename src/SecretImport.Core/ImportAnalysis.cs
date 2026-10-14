namespace SecretImport.Core;

/// <param name="Message">Human-readable text. Must never contain a secret value.</param>
public sealed record ImportWarning(ImportWarningKind Kind, string Message);

public sealed record ImportAnalysis(
    ProjectInfo Project, ImportRequest Request, string? SecretsPath,
    bool NeedsInit, bool CanInit, int ExistingCount, int IncomingCount,
    ImportPlan Plan, IReadOnlyList<ImportWarning> Warnings, string? ExistingFileHash);

public sealed record ImportResult(int Added, int Changed, int Removed, string SecretsPath, string? BackupPath);
