using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>The parsed command line of an import run (ADR-0008, ADR-0014).</summary>
/// <param name="ExitCode"><c>--exit-code</c>: with <c>--dry-run</c>, exit 4 when there are changes.</param>
/// <param name="Json"><c>--json</c>: print one JSON report instead of tables and prompts.</param>
public sealed record ImportOptions(
    string Project, string File, ImportMode Mode, string? Section, string? Scope,
    bool DryRun, bool Yes, bool Init, bool ShowValues, bool ExitCode = false, bool Json = false)
{
    /// <summary>The <c>--file</c> value that means "read the JSON from stdin".</summary>
    public const string StdinFile = "-";

    public bool ReadsStdin => File == StdinFile;
}
