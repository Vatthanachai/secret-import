using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>The human-readable output: Spectre tables and messages on <paramref name="console"/>, errors on <paramref name="errorConsole"/>.</summary>
public sealed class ConsoleImportOutput(IAnsiConsole console, IAnsiConsole errorConsole, bool showValues) : IImportOutput
{
    private ImportAnalysis? analysis;

    public bool CanPrompt => console.Profile.Capabilities.Interactive;

    public void Analyzed(ImportAnalysis analysis)
    {
        this.analysis = analysis;
    }

    public void Initialized(ProjectRef project)
    {
        console.WriteLine($"Initialized user secrets for '{project.Name}'.");
    }

    public void Review()
    {
        Render();
    }

    public int DryRun(int exitCode)
    {
        Render();
        console.WriteLine("Dry run: nothing was written.");
        return exitCode;
    }

    public int NoChanges()
    {
        Render();
        console.WriteLine("No changes to import.");
        return ExitCodes.Success;
    }

    public int Applied(ImportResult result)
    {
        console.WriteLine($"Imported: +{result.Added} added, ~{result.Changed} changed, -{result.Removed} removed.");
        console.WriteLine($"Secrets file: {result.SecretsPath}");
        if (result.BackupPath is not null)
        {
            console.WriteLine($"Backup: {result.BackupPath}");
        }

        return ExitCodes.Success;
    }

    public int ConfirmationRequired(string message)
    {
        if (analysis is not null)
        {
            Render();
        }

        WriteError(message);
        return ExitCodes.FromErrorKind(CliErrorKind.ConfirmationRequired);
    }

    public int Cancelled(string message)
    {
        WriteError(message);
        return ExitCodes.FromErrorKind(CliErrorKind.Cancelled);
    }

    public int Failed(CliErrorKind kind, string message, IReadOnlyList<ProjectRef> candidates)
    {
        WriteError(message);
        foreach (ProjectRef candidate in candidates)
        {
            errorConsole.WriteLine($"  {candidate.Name}  {candidate.Path}");
        }

        if (kind == CliErrorKind.MultipleProjects)
        {
            errorConsole.WriteLine("Pass one project file to --project.");
        }

        return ExitCodes.FromErrorKind(kind);
    }

    /// <summary>Only called once the flow has an analysis (after Analyze).</summary>
    private void Render()
    {
        AnalysisRenderer.Render(console, analysis!, showValues);
    }

    private void WriteError(string message)
    {
        errorConsole.Write(new Markup("[red]error:[/] "));
        errorConsole.WriteLine(message);
    }
}