using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// <c>--json</c> output (ADR-0014): every outcome writes exactly one <see cref="JsonReport"/> to <paramref name="stdout"/>,
/// the raw writer under the console, so Spectre never wraps or colors it. Nothing goes to stderr and nothing is ever prompted.
/// </summary>
public sealed class JsonImportOutput(TextWriter stdout, bool showValues) : IImportOutput
{
    private ImportAnalysis? analysis;
    private bool initialized;

    public bool CanPrompt => false;

    public void Analyzed(ImportAnalysis analysis)
    {
        this.analysis = analysis;
    }

    public void Initialized(ProjectRef project)
    {
        initialized = true;
    }

    public void Review()
    {
        // The report always carries the plan; there is nothing to show before applying.
    }

    public int DryRun(int exitCode)
    {
        return Write(JsonReportStatus.DryRun, exitCode);
    }

    public int NoChanges()
    {
        return Write(JsonReportStatus.NoChanges, ExitCodes.Success);
    }

    public int Applied(ImportResult result)
    {
        return Write(JsonReportStatus.Applied, ExitCodes.Success, result);
    }

    public int ConfirmationRequired(string message)
    {
        return Failed(CliErrorKind.ConfirmationRequired, message, []);
    }

    public int Cancelled(string message)
    {
        return Failed(CliErrorKind.Cancelled, message, []);
    }

    public int Failed(CliErrorKind kind, string message, IReadOnlyList<ProjectRef> candidates)
    {
        JsonReportStatus status = kind is CliErrorKind.ConfirmationRequired or CliErrorKind.Cancelled ? JsonReportStatus.Cancelled : JsonReportStatus.Error;
        JsonReportError error = new(kind, message, [.. candidates.Select(static c => new JsonReportProject(c.Name, c.Path))]);
        return Write(status, ExitCodes.FromErrorKind(kind), error: error);
    }

    private int Write(JsonReportStatus status, int exitCode, ImportResult? result = null, JsonReportError? error = null)
    {
        JsonReport report = analysis is null
            ? new JsonReport(exitCode, status, null, null, null, null, null, null, initialized, null, [], [], null, error)
            : new JsonReport(
                exitCode,
                status,
                new JsonReportProject(analysis.Project.Project.Name, analysis.Project.Project.Path),
                analysis.Project.UserSecretsId,
                result?.SecretsPath ?? analysis.SecretsPath,
                analysis.Request.Mode,
                analysis.Request.Section,
                analysis.Request.ScopePath,
                initialized,
                Summarize(analysis.Plan.Changes),
                [.. analysis.Plan.Changes.Select(ToReport)],
                [.. analysis.Warnings.Select(static w => new JsonReportWarning(w.Kind, w.Message))],
                result?.BackupPath,
                error);
        stdout.WriteLine(report.Serialize());
        stdout.Flush();
        return exitCode;
    }

    private JsonReportChange ToReport(SecretChange change)
    {
        return showValues
            ? new JsonReportChangeWithValues(change.Key, change.Kind, change.OldValue, change.NewValue)
            : new JsonReportChange(change.Key, change.Kind);
    }

    private static JsonReportSummary Summarize(IReadOnlyList<SecretChange> changes)
    {
        int Count(SecretChangeKind kind) => changes.Count(c => c.Kind == kind);
        return new JsonReportSummary(
            Count(SecretChangeKind.Added), Count(SecretChangeKind.Changed), Count(SecretChangeKind.Removed),
            Count(SecretChangeKind.Unchanged), Count(SecretChangeKind.Skipped));
    }
}