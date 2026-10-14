using Spectre.Console.Testing;

using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary>The confirm screen: id, path, counts, diff, warnings, and masking (ADR-0003, ADR-0005).</summary>
public sealed class AnalysisRendererTests : IDisposable
{
    private readonly TestConsole console = new TestConsole().Width(400);

    public void Dispose() => console.Dispose();

    [Fact]
    public void Render_ShowsTheIdPathCountsModeAndEveryChange()
    {
        ImportAnalysis analysis = TestData.Initialized(
        [
            .. TestData.SomeChanges(),
            new SecretChange("Gone", SecretChangeKind.Removed, "old", null),
            new SecretChange("Kept", SecretChangeKind.Skipped, "old", "new"),
        ]) with { Request = TestData.PlaceholderRequest with { Mode = ImportMode.Replace } };

        AnalysisRenderer.Render(console, analysis, showValues: false);

        string output = console.Output;
        Assert.Contains("App (/repo/src/App/App.csproj)", output);
        Assert.Contains("app-secrets-id", output);
        Assert.Contains("/secrets/app-secrets-id/secrets.json", output);
        Assert.Contains("replace", output);
        Assert.Contains("2 existing, 5 incoming", output);
        foreach (string key in new[] { "Api:Key", "Db:Password", "Same", "Gone", "Kept" })
        {
            Assert.Contains(key, output);
        }

        foreach (string kind in new[] { "Added", "Changed", "Unchanged", "Removed", "Skipped" })
        {
            Assert.Contains(kind, output);
        }

        Assert.Contains("+1 added, ~1 changed, -1 removed, 1 unchanged, 1 skipped", output);
        Assert.DoesNotContain("Shared with", output);
        Assert.DoesNotContain("Scope", output);
    }

    [Fact]
    public void Render_MasksValuesWithBullets_OnAUnicodeConsole()
    {
        console.SupportsUnicode(true);

        AnalysisRenderer.Render(console, TestData.Initialized(TestData.SomeChanges()), showValues: false);

        Assert.Contains(AnalysisRenderer.Mask, console.Output);
        Assert.DoesNotContain(TestData.SecretValue, console.Output);
        Assert.DoesNotContain(TestData.OldSecretValue, console.Output);
    }

    [Fact]
    public void Render_MasksValuesWithAsterisks_WhenTheConsoleCannotPrintUnicode()
    {
        console.SupportsUnicode(false);

        AnalysisRenderer.Render(console, TestData.Initialized(TestData.SomeChanges()), showValues: false);

        Assert.Contains(AnalysisRenderer.AsciiMask, console.Output);
        Assert.DoesNotContain(AnalysisRenderer.Mask, console.Output);
        Assert.DoesNotContain(TestData.SecretValue, console.Output);
    }

    [Fact]
    public void Render_ShowValues_PrintsTheValues()
    {
        AnalysisRenderer.Render(console, TestData.Initialized(TestData.SomeChanges()), showValues: true);

        Assert.Contains(TestData.SecretValue, console.Output);
        Assert.Contains(TestData.OldSecretValue, console.Output);
        Assert.DoesNotContain(AnalysisRenderer.AsciiMask, console.Output);
    }

    [Fact]
    public void Render_UninitializedProject_SaysSo()
    {
        AnalysisRenderer.Render(console, TestData.NotInitialized(true, TestData.SomeChanges()), showValues: false);

        Assert.Contains("(not initialized)", console.Output);
        Assert.Contains("(not created yet)", console.Output);
    }

    [Fact]
    public void Render_WithoutChanges_PrintsNoTable()
    {
        AnalysisRenderer.Render(console, TestData.Initialized(), showValues: false);

        Assert.DoesNotContain("Change", console.Output);
        Assert.Contains("+0 added", console.Output);
    }

    [Fact]
    public void Render_ShowsTheSectionWarningsAndSharedProjects()
    {
        ImportAnalysis analysis = TestData.Analysis(
            TestData.SomeChanges(),
            warnings:
            [
                new ImportWarning(ImportWarningKind.SharedSecretsId, "Other.csproj uses the same UserSecretsId."),
                new ImportWarning(ImportWarningKind.InsecureFilePermissions, "secrets.json is readable by [group]."),
            ],
            sharedWith: [new ProjectRef("/repo/Other/Other.csproj", "Other"), new ProjectRef("/repo/Web/Web.csproj", "Web")])
            with { Request = TestData.PlaceholderRequest with { Section = "ConnectionStrings" } };

        AnalysisRenderer.Render(console, analysis, showValues: false);

        Assert.Contains("placeholder (section 'ConnectionStrings')", console.Output);
        Assert.Matches(@"Shared with:\s+Other, Web", console.Output);
        Assert.Contains("warning: Other.csproj uses the same UserSecretsId.", console.Output);
        Assert.Contains("warning: secrets.json is readable by [group].", console.Output);
    }

    [Fact]
    public void Render_ShowsTheScope_OnlyWhenOneWasUsed()
    {
        ImportAnalysis withScope = TestData.Initialized() with { Request = TestData.PlaceholderRequest with { ScopePath = "/repo/All.slnx" } };

        AnalysisRenderer.Render(console, withScope, showValues: false);

        Assert.Matches(@"Scope:\s+/repo/All.slnx", console.Output);
    }

    [Fact]
    public void Render_DoesNotInterpretMarkupInKeysOrValues()
    {
        AnalysisRenderer.Render(console, TestData.Initialized(new SecretChange("[red]Key[/]", SecretChangeKind.Added, null, "[bold]v[/]")), showValues: true);

        Assert.Contains("[red]Key[/]", console.Output);
        Assert.Contains("[bold]v[/]", console.Output);
    }
}
