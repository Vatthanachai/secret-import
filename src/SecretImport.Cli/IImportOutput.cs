using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// How <see cref="ImportFlow"/> reports a run: <see cref="ConsoleImportOutput"/> for people, <see cref="JsonImportOutput"/> for <c>--json</c> (ADR-0014).
/// The flow calls <see cref="Analyzed"/> after every Analyze, then exactly one outcome method, which returns the exit code.
/// </summary>
public interface IImportOutput
{
    /// <summary>Whether the flow may ask the user a question on the console.</summary>
    bool CanPrompt { get; }

    /// <summary>Records the latest analysis; later outcomes describe it.</summary>
    void Analyzed(ImportAnalysis analysis);

    /// <summary><c>dotnet user-secrets init</c> ran for <paramref name="project"/>.</summary>
    void Initialized(ProjectRef project);

    /// <summary>The plan is about to be confirmed or applied.</summary>
    void Review();

    int DryRun(int exitCode);

    int NoChanges();

    int Applied(ImportResult result);

    /// <summary>A confirmation was needed but can't be asked for; <paramref name="message"/> names the missing flags.</summary>
    int ConfirmationRequired(string message);

    /// <summary>The user said no, or the run was cancelled.</summary>
    int Cancelled(string message);

    /// <summary>The run failed. <paramref name="message"/> must never contain a secret value.</summary>
    int Failed(CliErrorKind kind, string message, IReadOnlyList<ProjectRef> candidates);
}