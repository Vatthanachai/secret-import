using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>The parsed command line of an import run (ADR-0008).</summary>
public sealed record ImportOptions(
    string Project, string File, ImportMode Mode, string? Section,
    bool DryRun, bool Yes, bool Init, bool ShowValues)
{
    /// <summary>The <c>--file</c> value that means "read the JSON from stdin".</summary>
    public const string StdinFile = "-";

    public bool ReadsStdin => File == StdinFile;
}
