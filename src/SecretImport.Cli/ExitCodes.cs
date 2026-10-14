using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>Process exit codes of <c>secret-import</c> (ADR-0008).</summary>
public static class ExitCodes
{
    /// <summary>Success, nothing to change, or any <c>--dry-run</c>.</summary>
    public const int Success = 0;

    /// <summary>Invalid input: bad JSON, duplicate keys, missing section, several projects, bad options.</summary>
    public const int InvalidInput = 1;

    /// <summary>The user cancelled, or a confirmation was needed but not given (<c>--yes</c> / <c>--init</c>).</summary>
    public const int Cancelled = 2;

    /// <summary>Environment or I/O failure: missing SDK, unwritable file, <c>secrets.json</c> changed after Analyze.</summary>
    public const int EnvironmentError = 3;

    public static int FromErrorKind(ImportErrorKind kind)
    {
        return kind switch
        {
            ImportErrorKind.InvalidInput or ImportErrorKind.NoProjectFound or ImportErrorKind.MultipleProjects => InvalidInput,
            ImportErrorKind.SdkUnavailable or ImportErrorKind.ConcurrentModification or ImportErrorKind.IoFailure => EnvironmentError,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown import error kind."),
        };
    }
}
