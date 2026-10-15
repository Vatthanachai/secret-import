using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>
/// Runs one <c>secret-import export</c> (ADR-0015): <see cref="IImportService.ExportAsync"/>, then either the raw JSON on stdout
/// (<c>--output -</c>) or <see cref="IImportService.WriteExportAsync"/> and a one-line summary without values.
/// Warnings and errors always go to <paramref name="errorConsole"/> (stderr), so stdout is only ever the JSON or the summary. Never prompts.
/// </summary>
public sealed class ExportFlow(IAnsiConsole console, IAnsiConsole errorConsole, IImportService importService)
{
    public async Task<int> RunAsync(ExportOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunCoreAsync(options, cancellationToken);
        }
        catch (ImportException ex)
        {
            return ConsoleMessages.Failed(errorConsole, ex.Kind.ToCliErrorKind(), ex.Message, ex.Candidates);
        }
        catch (OperationCanceledException)
        {
            return ConsoleMessages.Failed(errorConsole, CliErrorKind.Cancelled, "Cancelled.", []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ConsoleMessages.Failed(errorConsole, CliErrorKind.IoFailure, ex.Message, []);
        }
    }

    private async Task<int> RunCoreAsync(ExportOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Output))
        {
            return ConsoleMessages.Failed(errorConsole, CliErrorKind.InvalidOptions, "--output needs a file path, or '-' for stdout.", []);
        }

        ExportResult result = await importService.ExportAsync(new ExportRequest(options.Project, options.Section, options.Flat), ct);
        foreach (ImportWarning warning in result.Warnings)
        {
            ConsoleMessages.WriteWarning(errorConsole, warning.Message);
        }

        if (options.WritesStdout)
        {
            // The raw writer under the console: Spectre must not wrap, color or parse markup in the JSON.
            TextWriter stdout = console.Profile.Out.Writer;
            await stdout.WriteAsync(result.Json.AsMemory(), ct);
            await stdout.FlushAsync(ct);
            return ExitCodes.Success;
        }

        await importService.WriteExportAsync(options.Output, result.Json, options.Force, ct);
        string keys = result.KeyCount == 1 ? "1 key" : $"{result.KeyCount} keys";

        // A plain line on the raw writer so Spectre never wraps the path (it wraps at 80 columns when stdout is redirected).
        TextWriter writer = console.Profile.Out.Writer;
        await writer.WriteLineAsync($"Exported {keys} from {result.Project.Project.Name} to {Path.GetFullPath(options.Output)}".AsMemory(), ct);
        await writer.FlushAsync(ct);
        return ExitCodes.Success;
    }
}
