using Spectre.Console;
using Spectre.Console.Rendering;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>Renders the confirm screen of an <see cref="ImportAnalysis"/> (ADR-0005): id, path, counts, the diff and warnings.</summary>
public static class AnalysisRenderer
{
    /// <summary>Shown in place of a secret value unless <c>--show-values</c> is set (ADR-0003).</summary>
    public const string Mask = "••••";

    /// <summary>Used instead of <see cref="Mask"/> when the console can't print Unicode.</summary>
    public const string AsciiMask = "****";

    public static void Render(IAnsiConsole console, ImportAnalysis analysis, bool showValues)
    {
        ProjectInfo project = analysis.Project;
        ImportRequest request = analysis.Request;

        Grid summary = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn();
        AddRow(summary, "Project", $"{project.Project.Name} ({project.Project.Path})");
        AddRow(summary, "JSON", request.Section is null ? request.Json.DisplayName : $"{request.Json.DisplayName} (section '{request.Section}')");
        AddRow(summary, "UserSecretsId", project.UserSecretsId ?? "(not initialized)");
        AddRow(summary, "Secrets file", analysis.SecretsPath ?? "(not created yet)");
        AddRow(summary, "Mode", request.Mode.ToString().ToLowerInvariant());
        AddRow(summary, "Keys", $"{analysis.ExistingCount} existing, {analysis.IncomingCount} incoming");
        if (project.SharedWith.Count > 0)
        {
            AddRow(summary, "Shared with", string.Join(", ", project.SharedWith.Select(static p => p.Name)));
        }

        console.Write(summary);
        console.WriteLine();

        IReadOnlyList<SecretChange> changes = analysis.Plan.Changes;
        if (changes.Count > 0)
        {
            string mask = console.Profile.Capabilities.Unicode ? Mask : AsciiMask;
            Table table = new Table()
                .AddColumn("Key")
                .AddColumn("Change")
                .AddColumn("Current")
                .AddColumn("New");
            foreach (SecretChange change in changes)
            {
                table.AddRow(
                    new Text(change.Key),
                    new Text(change.Kind.ToString(), KindStyle(change.Kind)),
                    new Text(Display(change.OldValue, showValues, mask)),
                    new Text(Display(change.NewValue, showValues, mask)));
            }

            console.Write(table);
        }

        console.WriteLine(Totals(changes));

        foreach (ImportWarning warning in analysis.Warnings)
        {
            console.Write(new Markup("[yellow]warning:[/] "));
            console.WriteLine(warning.Message);
        }
    }

    private static void AddRow(Grid grid, string label, string value)
    {
        grid.AddRow(new IRenderable[] { new Text(label + ":", new Style(decoration: Decoration.Bold)), new Text(value) });
    }

    private static string Display(string? value, bool showValues, string mask)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return showValues ? value : mask;
    }

    private static Style KindStyle(SecretChangeKind kind)
    {
        return kind switch
        {
            SecretChangeKind.Added => new Style(Color.Green),
            SecretChangeKind.Changed => new Style(Color.Yellow),
            SecretChangeKind.Removed => new Style(Color.Red),
            _ => new Style(Color.Grey),
        };
    }

    private static string Totals(IReadOnlyList<SecretChange> changes)
    {
        int Count(SecretChangeKind kind) => changes.Count(c => c.Kind == kind);
        return $"+{Count(SecretChangeKind.Added)} added, ~{Count(SecretChangeKind.Changed)} changed, -{Count(SecretChangeKind.Removed)} removed, "
            + $"{Count(SecretChangeKind.Unchanged)} unchanged, {Count(SecretChangeKind.Skipped)} skipped";
    }
}
