using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Drives one import through <see cref="IImportService"/>: Analyze → (init) → confirm → Apply, and maps the outcome to an exit code (ADR-0005, ADR-0008).
/// Every outcome is reported through an <see cref="IImportOutput"/>: Spectre text on <paramref name="console"/> / <paramref name="errorConsole"/>,
/// or with <c>--json</c> one JSON report on the raw stdout writer (ADR-0014).
/// </summary>
/// <param name="scopeResolver">Core's rule for the shared-id scope when <c>--scope</c> is omitted (ADR-0013); defaults to the real file system.</param>
public sealed class ImportFlow(
    IAnsiConsole console, IAnsiConsole errorConsole, TextReader stdin, IImportService importService, SolutionScopeResolver? scopeResolver = null)
{
    private readonly SolutionScopeResolver scopeResolver = scopeResolver ?? new SolutionScopeResolver();

    public const string StdinDisplayName = "stdin";

    public async Task<int> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        IImportOutput output = options.Json
            ? new JsonImportOutput(console.Profile.Out.Writer, options.ShowValues)
            : new ConsoleImportOutput(console, errorConsole, options.ShowValues);
        try
        {
            return await RunCoreAsync(options, output, cancellationToken);
        }
        catch (ImportException ex)
        {
            return output.Failed(ex.Kind.ToCliErrorKind(), ex.Message, ex.Candidates);
        }
        catch (OperationCanceledException)
        {
            return output.Cancelled("Cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return output.Failed(CliErrorKind.IoFailure, ex.Message, []);
        }
    }

    private async Task<int> RunCoreAsync(ImportOptions options, IImportOutput output, CancellationToken ct)
    {
        if (options.ExitCode && !options.DryRun)
        {
            return output.Failed(CliErrorKind.InvalidOptions, "--exit-code can only be used with --dry-run.", []);
        }

        if (options.ReadsStdin && !options.DryRun && !options.Yes)
        {
            return output.ConfirmationRequired("--file - reads the JSON from stdin, so there is no way to confirm interactively. "
                + "Pass --yes (and --init if the project has no UserSecretsId yet), or preview with --dry-run.");
        }

        JsonInput json = options.ReadsStdin
            ? new JsonInput(StdinDisplayName, await stdin.ReadToEndAsync(ct))
            : JsonInput.FromFile(options.File);
        ImportRequest request = new(options.Project, json, options.Mode, options.Section, scopeResolver.Resolve(options.Project, options.Scope));
        ImportAnalysis analysis = await importService.AnalyzeAsync(request, ct);
        output.Analyzed(analysis);

        if (options.DryRun)
        {
            return output.DryRun(options.ExitCode && analysis.Plan.HasChanges ? ExitCodes.ChangesPending : ExitCodes.Success);
        }

        if (!analysis.Plan.HasChanges)
        {
            return output.NoChanges();
        }

        ProjectRef project = analysis.Project.Project;
        if (analysis.NeedsInit && !analysis.CanInit)
        {
            return output.Failed(
                CliErrorKind.SdkUnavailable,
                $"Project '{project.Name}' has no UserSecretsId and can't be initialized because the .NET SDK is unavailable. "
                    + $"Install the SDK, or run: dotnet user-secrets init --project \"{project.Path}\"",
                []);
        }

        List<string> missingFlags = [];
        if (analysis.NeedsInit && !options.Init)
        {
            missingFlags.Add("--init");
        }

        if (!options.Yes)
        {
            missingFlags.Add("--yes");
        }

        if (missingFlags.Count > 0 && (!output.CanPrompt || options.ReadsStdin))
        {
            string reason = options.ReadsStdin ? "stdin is used for the JSON input"
                : options.Json ? "--json never prompts"
                : "the session is not interactive";
            return output.ConfirmationRequired($"Confirmation is required but {reason}. Pass {string.Join(" and ", missingFlags)} to continue.");
        }

        if (analysis.NeedsInit)
        {
            if (!options.Init && !await ConfirmAsync(
                    $"Project '{project.Name}' has no UserSecretsId. Run 'dotnet user-secrets init'? This edits the project file.", ct))
            {
                return output.Cancelled("Cancelled: the project was not initialized.");
            }

            await importService.InitAsync(project, ct);
            output.Initialized(project);
            analysis = await importService.AnalyzeAsync(request, ct);
            output.Analyzed(analysis);
        }

        output.Review();
        if (!options.Yes && !await ConfirmAsync($"Import into {analysis.SecretsPath ?? project.Name}?", ct))
        {
            return output.Cancelled("Cancelled: nothing was written.");
        }

        ImportResult result = await importService.ApplyAsync(analysis, ct);
        return output.Applied(result);
    }

    private Task<bool> ConfirmAsync(string question, CancellationToken ct)
    {
        return new ConfirmationPrompt(Markup.Escape(question)) { DefaultValue = false }.ShowAsync(console, ct);
    }
}