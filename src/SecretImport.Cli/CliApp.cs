using System.CommandLine;
using System.CommandLine.Help;

using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Testable entry point of the <c>secret-import</c> tool (ADR-0008). <c>Program.cs</c> only delegates here via <see cref="CliHost"/>.
/// Normal output goes to <paramref name="console"/> (stdout); errors, including parse errors, go to <paramref name="errorConsole"/> (stderr).
/// With <c>--json</c> everything after parsing is one JSON report on stdout (ADR-0014).
/// </summary>
/// <param name="scopeResolver">Picks the shared-id scope when <c>--scope</c> is omitted; defaults to Core's resolver over the real file system.</param>
public sealed class CliApp(
    IAnsiConsole console, IAnsiConsole errorConsole, TextReader stdin, IImportService importService, SolutionScopeResolver? scopeResolver = null)
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
            + "Defaults to --project when it is a folder or solution, else to the nearest .sln/.slnx above the project file (a lone .slnx wins over .sln files next to it).",
        HelpName = "path",
    };

    private readonly Option<bool> dryRunOption = new("--dry-run")
    {
        Description = "Show the diff without writing anything. Exits 0, or 4 when there are changes and --exit-code is set.",
    };

    private readonly Option<bool> exitCodeOption = new("--exit-code")
    {
        Description = "With --dry-run, exit 4 when there are changes to import and 0 when there are none. Only valid with --dry-run.",
    };

    private readonly Option<bool> yesOption = new("--yes", "-y") { Description = "Import without asking for confirmation." };

    private readonly Option<bool> initOption = new("--init") { Description = "Run 'dotnet user-secrets init' without asking if the project has no UserSecretsId." };

    private readonly Option<bool> showValuesOption = new("--show-values") { Description = "Show secret values in the diff instead of masking them." };

    private readonly Option<bool> jsonOption = new("--json")
    {
        Description = "Print one JSON object with the result to stdout instead of tables. Never prompts, so pass --yes (and --init) to import. "
            + "Values are included only with --show-values.",
    };

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
            exitCodeOption,
            yesOption,
            initOption,
            showValuesOption,
            jsonOption,
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
            parseResult.GetValue(showValuesOption),
            parseResult.GetValue(exitCodeOption),
            parseResult.GetValue(jsonOption));
        return new ImportFlow(console, errorConsole, stdin, importService, scopeResolver).RunAsync(options, cancellationToken);
    }
}
