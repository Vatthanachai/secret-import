using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>Process exit codes of <c>secret-import</c> (ADR-0008, ADR-0014).</summary>
public static class ExitCodes
{
    /// <summary>Success, nothing to change, or a <c>--dry-run</c> (without <c>--exit-code</c>, or with it and no changes).</summary>
    public const int Success = 0;

    /// <summary>Invalid input: bad JSON, duplicate keys, missing section, several projects, bad options.</summary>
    public const int InvalidInput = 1;

    /// <summary>The user cancelled, or a confirmation was needed but not given (<c>--yes</c> / <c>--init</c>).</summary>
    public const int Cancelled = 2;

    /// <summary>Environment or I/O failure: missing SDK, unwritable file, <c>secrets.json</c> changed after Analyze.</summary>
    public const int EnvironmentError = 3;

    /// <summary><c>--dry-run --exit-code</c> found changes to import (ADR-0014).</summary>
    public const int ChangesPending = 4;

    public static int FromErrorKind(ImportErrorKind kind)
    {
        return FromErrorKind(kind.ToCliErrorKind());
    }

    public static int FromErrorKind(CliErrorKind kind)
    {
        return kind switch
        {
            CliErrorKind.InvalidInput or CliErrorKind.NoProjectFound or CliErrorKind.MultipleProjects or CliErrorKind.InvalidOptions => InvalidInput,
            CliErrorKind.ConfirmationRequired or CliErrorKind.Cancelled => Cancelled,
            CliErrorKind.SdkUnavailable or CliErrorKind.ConcurrentModification or CliErrorKind.IoFailure => EnvironmentError,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown error kind."),
        };
    }
}
