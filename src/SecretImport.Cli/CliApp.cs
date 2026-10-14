using System.CommandLine;
using System.CommandLine.Help;

using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Testable entry point of the <c>secret-import</c> tool (ADR-0008). <c>Program.cs</c> only delegates here via <see cref="CliHost"/>.
/// Normal output goes to <paramref name="console"/> (stdout); errors, including parse errors, go to <paramref name="errorConsole"/> (stderr).
/// </summary>
public sealed class CliApp(IAnsiConsole console, IAnsiConsole errorConsole, TextReader stdin, IImportService importService)
{
    public const string CommandName = "secret-import";

    private readonly Option<string> projectOption = new("--project", "-p")
    {
        Description = "The project to import into: a .csproj/.fsproj/.vbproj, a folder, or a .sln/.slnx that resolves to exactly one project.",
        HelpName = "path",
        Required = true,
    };

    private readonly Option<string> fileOption = new("--file", "-f")
    {
        Description = "The JSON file to import, or '-' to read it from stdin (needs --yes unless --dry-run).",
        HelpName = "json|-",
        Required = true,
    };

    private readonly Option<ImportMode> modeOption = new("--mode", "-m")
    {
        Description = "overwrite: incoming values win. skip: existing values win. replace: also remove keys not in the JSON.",
        HelpName = "overwrite|skip|replace",
        DefaultValueFactory = static _ => ImportMode.Overwrite,
    };

    private readonly Option<string?> sectionOption = new("--section", "-s")
    {
        Description = "Import only this section of the JSON, e.g. 'ConnectionStrings' or 'App:Auth'.",
        HelpName = "path",
    };

    private readonly Option<string?> scopeOption = new("--scope")
    {
        Description = "A folder or .sln/.slnx whose projects are checked for a UserSecretsId shared with --project. "
            + "Defaults to --project when it is a folder or solution, else to the nearest single .sln/.slnx above the project file.",
        HelpName = "path",
    };

    private readonly Option<bool> dryRunOption = new("--dry-run") { Description = "Show the diff without writing anything (always exits 0)." };

    private readonly Option<bool> yesOption = new("--yes", "-y") { Description = "Import without asking for confirmation." };

    private readonly Option<bool> initOption = new("--init") { Description = "Run 'dotnet user-secrets init' without asking if the project has no UserSecretsId." };

    private readonly Option<bool> showValuesOption = new("--show-values") { Description = "Show secret values in the diff instead of masking them." };

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Command root = BuildRootCommand();
        InvocationConfiguration configuration = new()
        {
            Output = console.Profile.Out.Writer,
            Error = errorConsole.Profile.Out.Writer,
        };
        return await root.Parse(args).InvokeAsync(configuration, cancellationToken);
    }

    private Command BuildRootCommand()
    {
        // A named root keeps help output stable regardless of the host executable name.
        Command root = new(CommandName, "Import a JSON file into a .NET project's dotnet user-secrets store.")
        {
            new HelpOption(),
            new VersionOption(),
            projectOption,
            fileOption,
            modeOption,
            sectionOption,
            scopeOption,
            dryRunOption,
            yesOption,
            initOption,
            showValuesOption,
        };
        root.SetAction(InvokeImportAsync);
        return root;
    }

    private Task<int> InvokeImportAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        ImportOptions options = new(
            parseResult.GetRequiredValue(projectOption),
            parseResult.GetRequiredValue(fileOption),
            parseResult.GetValue(modeOption),
            parseResult.GetValue(sectionOption),
            parseResult.GetValue(scopeOption),
            parseResult.GetValue(dryRunOption),
            parseResult.GetValue(yesOption),
            parseResult.GetValue(initOption),
            parseResult.GetValue(showValuesOption));
        return new ImportFlow(console, errorConsole, stdin, importService).RunAsync(options, cancellationToken);
    }
}
