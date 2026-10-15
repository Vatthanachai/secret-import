using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>The single object <c>--json</c> prints. Property names are a contract with scripts: renaming or removing one needs a new ADR (ADR-0014).</summary>
public sealed record JsonReport(
    int ExitCode,
    JsonReportStatus Status,
    JsonReportProject? Project,
    string? UserSecretsId,
    string? SecretsPath,
    ImportMode? Mode,
    string? Section,
    string? Scope,
    bool Initialized,
    JsonReportSummary? Summary,
    IReadOnlyList<JsonReportChange> Changes,
    IReadOnlyList<JsonReportWarning> Warnings,
    string? BackupPath,
    JsonReportError? Error)
{
    /// <summary>camelCase properties and enums, indented, and no HTML escaping since the output is never embedded in a page.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }
}

public enum JsonReportStatus { DryRun, NoChanges, Applied, Cancelled, Error }

public sealed record JsonReportProject(string Name, string Path);

public sealed record JsonReportSummary(int Added, int Changed, int Removed, int Unchanged, int Skipped);

/// <summary>A change without values. <see cref="JsonReportChangeWithValues"/> adds them for <c>--show-values</c>.</summary>
[JsonDerivedType(typeof(JsonReportChangeWithValues))]
public record JsonReportChange(string Key, SecretChangeKind Kind);

/// <summary>Only produced with <c>--show-values</c>; both values are written, as <c>null</c> when absent.</summary>
public sealed record JsonReportChangeWithValues(string Key, SecretChangeKind Kind, string? OldValue, string? NewValue) : JsonReportChange(Key, Kind);

public sealed record JsonReportWarning(ImportWarningKind Kind, string Message);

public sealed record JsonReportError(CliErrorKind Kind, string Message, IReadOnlyList<JsonReportProject> Candidates);