using System.CommandLine;
using System.CommandLine.Help;

using Spectre.Console;

namespace SecretImport.Cli;

/// <summary>Testable entry point of the <c>secret-import</c> tool. <c>Program.cs</c> only delegates here.</summary>
public sealed class CliApp(IAnsiConsole console)
{
    public const string CommandName = "secret-import";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Command root = BuildRootCommand();
        TextWriter writer = console.Profile.Out.Writer;
        InvocationConfiguration configuration = new() { Output = writer, Error = writer };
        return await root.Parse(args).InvokeAsync(configuration, cancellationToken);
    }

    private static Command BuildRootCommand()
    {
        // A named root keeps help output stable regardless of the host executable name.
        // Phase 0 only: the options and the import action arrive in Phase 6 (ADR-0008).
        return new Command(CommandName, "Import a JSON file into a .NET project's dotnet user-secrets store.")
        {
            new HelpOption(),
            new VersionOption(),
        };
    }
}
