using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>The <c>warning:</c> / <c>error:</c> lines shared by import and export. Messages are printed as plain text, never as markup.</summary>
public static class ConsoleMessages
{
    public static void WriteWarning(IAnsiConsole console, string message)
    {
        console.Write(new Markup("[yellow]warning:[/] "));
        console.WriteLine(message);
    }

    public static void WriteError(IAnsiConsole errorConsole, string message)
    {
        errorConsole.Write(new Markup("[red]error:[/] "));
        errorConsole.WriteLine(message);
    }

    /// <summary>
    /// Prints <paramref name="message"/> as an error, then one line per candidate project and, for <see cref="CliErrorKind.MultipleProjects"/>,
    /// a hint to pass one project file (ADR-0006). Returns the exit code for <paramref name="kind"/>.
    /// </summary>
    public static int Failed(IAnsiConsole errorConsole, CliErrorKind kind, string message, IReadOnlyList<ProjectRef> candidates)
    {
        WriteError(errorConsole, message);
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
}
