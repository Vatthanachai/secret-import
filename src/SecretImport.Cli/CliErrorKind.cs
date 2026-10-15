using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Why a run failed or stopped: every <see cref="ImportErrorKind"/> plus the kinds only the CLI produces (ADR-0014).
/// Reported as <c>error.kind</c> in camelCase by <c>--json</c>.
/// </summary>
public enum CliErrorKind
{
    InvalidInput,
    NoProjectFound,
    MultipleProjects,
    SdkUnavailable,
    ConcurrentModification,
    IoFailure,

    /// <summary>A confirmation was needed but <c>--yes</c> / <c>--init</c> wasn't given and prompting isn't possible.</summary>
    ConfirmationRequired,

    /// <summary>The user answered "no", or the run was cancelled.</summary>
    Cancelled,

    /// <summary>A combination of options that isn't allowed, e.g. <c>--exit-code</c> without <c>--dry-run</c>.</summary>
    InvalidOptions,
}

public static class CliErrorKindExtensions
{
    public static CliErrorKind ToCliErrorKind(this ImportErrorKind kind)
    {
        return kind switch
        {
            ImportErrorKind.InvalidInput => CliErrorKind.InvalidInput,
            ImportErrorKind.NoProjectFound => CliErrorKind.NoProjectFound,
            ImportErrorKind.MultipleProjects => CliErrorKind.MultipleProjects,
            ImportErrorKind.SdkUnavailable => CliErrorKind.SdkUnavailable,
            ImportErrorKind.ConcurrentModification => CliErrorKind.ConcurrentModification,
            ImportErrorKind.IoFailure => CliErrorKind.IoFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown import error kind."),
        };
    }
}
