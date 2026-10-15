namespace SecretImport.Cli;

/// <summary>The parsed command line of <c>secret-import export</c> (ADR-0015).</summary>
/// <param name="Output">The file to write, or <see cref="StdoutOutput"/> to print the JSON to stdout.</param>
/// <param name="Force"><c>--force</c>: overwrite <paramref name="Output"/> when it already exists.</param>
public sealed record ExportOptions(string Project, string Output, string? Section, bool Flat, bool Force)
{
    /// <summary>The <c>--output</c> value that means "print the JSON to stdout".</summary>
    public const string StdoutOutput = "-";

    public bool WritesStdout => Output == StdoutOutput;
}
