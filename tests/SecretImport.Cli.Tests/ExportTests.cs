using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary><c>secret-import export</c> (ADR-0015): JSON to stdout or a file, warnings and errors on stderr, ADR-0008 exit codes.</summary>
public sealed class ExportTests : IDisposable
{
    private readonly CliHarness cli = new();

    public void Dispose() => cli.Dispose();

    private Task<int> ExportAsync(params string[] extra)
    {
        return cli.RunAsync(["export", "--project", TestData.Project.Path, .. extra]);
    }

    /// <summary>Runs over consoles built exactly like the real host's, on top of plain <see cref="StringWriter"/>s.</summary>
    private async Task<(int ExitCode, string Stdout, string Stderr)> RunOverRawWritersAsync(params string[] args)
    {
        StringWriter stdout = new();
        StringWriter stderr = new();
        IAnsiConsole outConsole = CliHost.CreateConsole(stdout, interactive: false);
        IAnsiConsole errConsole = CliHost.CreateConsole(stderr, interactive: false);
        int exitCode = await new CliApp(outConsole, errConsole, new StringReader(""), cli.Service).RunAsync(args, TestContext.Current.CancellationToken);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private void AssertNoSecretValue()
    {
        Assert.DoesNotContain(TestData.SecretValue, cli.Out.Output);
        Assert.DoesNotContain(TestData.SecretValue, cli.Err.Output);
    }

    // ---- --output - ----

    [Fact]
    public async Task OutputDash_WritesExactlyTheJsonFromCoreToStdout()
    {
        (int exitCode, string stdout, string stderr) = await RunOverRawWritersAsync("export", "--project", TestData.Project.Path, "--output", "-");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(TestData.ExportJson, stdout);
        Assert.Empty(stderr);
        ExportRequest request = Assert.Single(cli.Service.ExportRequests);
        Assert.Equal(new ExportRequest(TestData.Project.Path, null, false), request);
        Assert.Empty(cli.Service.WriteExportCalls);
    }

    [Fact]
    public async Task OutputDash_WithWarnings_KeepsStdoutPureJson_AndWarnsOnStderr()
    {
        cli.Service.ExportResult = TestData.Export(warnings:
        [
            new ImportWarning(ImportWarningKind.InsecureFilePermissions, "secrets.json has mode 644."),
            new ImportWarning(ImportWarningKind.XmlFallbackUsed, "Read the id from XML."),
        ]);

        (int exitCode, string stdout, string stderr) = await RunOverRawWritersAsync("export", "-p", TestData.Project.Path, "-o", "-");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(TestData.ExportJson, stdout);
        // The console may color "warning:" with ANSI codes, so check the label and the message separately.
        Assert.Equal(2, stderr.Split("warning:").Length - 1);
        Assert.Contains("secrets.json has mode 644.", stderr);
        Assert.Contains("Read the id from XML.", stderr);
        Assert.DoesNotContain(TestData.SecretValue, stderr);
    }

    [Fact]
    public async Task OutputDash_OverTestConsole_PrintsTheJsonOnly()
    {
        int exitCode = await ExportAsync("--output", "-");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(TestData.ExportJson.ReplaceLineEndings(), cli.Out.Output.ReplaceLineEndings());
        Assert.Empty(cli.Err.Output);
    }

    [Fact]
    public async Task SectionAndFlat_ArePassedToCore()
    {
        await cli.RunAsync("export", "-p", "some/dir", "-o", "-", "-s", "App:Auth", "--flat");

        Assert.Equal(new ExportRequest("some/dir", "App:Auth", true), Assert.Single(cli.Service.ExportRequests));
    }

    // ---- --output <file> ----

    [Fact]
    public async Task OutputFile_WritesThroughCore_WithoutOverwrite_AndPrintsASummaryWithoutValues()
    {
        int exitCode = await ExportAsync("--output", "out/secrets.json");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(new WriteExportCall("out/secrets.json", TestData.ExportJson, false), Assert.Single(cli.Service.WriteExportCalls));
        string summary = cli.Out.Output;
        Assert.Contains("Exported 2 keys", summary);
        Assert.Contains(TestData.Project.Name, summary);
        Assert.Contains(Path.GetFullPath("out/secrets.json"), summary);
        Assert.Empty(cli.Err.Output);
        AssertNoSecretValue();
    }

    [Fact]
    public async Task OutputFile_Force_AllowsOverwriting()
    {
        int exitCode = await ExportAsync("-o", "out.json", "--force");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(Assert.Single(cli.Service.WriteExportCalls).Overwrite);
    }

    [Fact]
    public async Task OutputFile_OneKey_UsesTheSingular()
    {
        cli.Service.ExportResult = TestData.Export(keyCount: 1);

        await ExportAsync("-o", "out.json");

        Assert.Contains("Exported 1 key from", cli.Out.Output);
    }

    [Fact]
    public async Task OutputFile_Warnings_GoToStderr()
    {
        cli.Service.ExportResult = TestData.Export(warnings: new ImportWarning(ImportWarningKind.InsecureFilePermissions, "mode 644"));

        int exitCode = await ExportAsync("-o", "out.json");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("warning: mode 644", cli.Err.Output);
        Assert.DoesNotContain("mode 644", cli.Out.Output);
        AssertNoSecretValue();
    }

    [Fact]
    public async Task OutputFile_ThatExists_WithoutForce_ReturnsOne_AndPrintsNoSummary()
    {
        cli.Service.WriteExportException = new ImportException(ImportErrorKind.InvalidInput, "out.json already exists. Choose another file or allow overwriting it (--force).");

        int exitCode = await ExportAsync("-o", "out.json");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("error: out.json already exists", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
        AssertNoSecretValue();
    }

    // ---- errors ----

    [Theory]
    [InlineData(ImportErrorKind.InvalidInput, ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.NoProjectFound, ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.MultipleProjects, ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.SdkUnavailable, ExitCodes.EnvironmentError)]
    [InlineData(ImportErrorKind.ConcurrentModification, ExitCodes.EnvironmentError)]
    [InlineData(ImportErrorKind.IoFailure, ExitCodes.EnvironmentError)]
    public async Task ExportError_MapsToTheExitCode_AndIsReportedOnStderr(ImportErrorKind kind, int expected)
    {
        cli.Service.ExportException = new ImportException(kind, $"boom {kind}");

        int exitCode = await ExportAsync("-o", "-");

        Assert.Equal(expected, exitCode);
        Assert.Contains($"error: boom {kind}", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
        Assert.Empty(cli.Service.WriteExportCalls);
    }

    [Fact]
    public async Task WriteError_IoFailure_ReturnsThree()
    {
        cli.Service.WriteExportException = new ImportException(ImportErrorKind.IoFailure, "Cannot write out.json: denied");

        int exitCode = await ExportAsync("-o", "out.json");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("error: Cannot write out.json: denied", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Fact]
    public async Task MultipleProjects_ListsTheCandidatesOnStderr()
    {
        ProjectRef a = new("/repo/A/A.csproj", "A");
        ProjectRef b = new("/repo/B/B.csproj", "B");
        cli.Service.ExportException = new ImportException(ImportErrorKind.MultipleProjects, "/repo resolves to 2 projects.", [a, b]);

        int exitCode = await ExportAsync("-o", "-");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("A  /repo/A/A.csproj", cli.Err.Output);
        Assert.Contains("B  /repo/B/B.csproj", cli.Err.Output);
        Assert.Contains("Pass one project file to --project.", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task IoExceptions_ReturnThree(Type exceptionType)
    {
        cli.Service.WriteExportException = (Exception)Activator.CreateInstance(exceptionType, "disk said no")!;

        int exitCode = await ExportAsync("-o", "out.json");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("error: disk said no", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Fact]
    public async Task Cancellation_ReturnsTwo()
    {
        cli.Service.ExportException = new OperationCanceledException();

        int exitCode = await ExportAsync("-o", "-");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("Cancelled", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankOutput_ReturnsOne_WithoutCallingCore(string output)
    {
        int exitCode = await ExportAsync("-o", output);

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--output", cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }

    // ---- parsing ----

    [Fact]
    public async Task MissingOutput_IsAParseError_ReturningOne()
    {
        int exitCode = await ExportAsync();

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--output", cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }

    [Fact]
    public async Task MissingProject_IsAParseError_ReturningOne()
    {
        int exitCode = await cli.RunAsync("export", "-o", "-");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--project", cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }

    [Theory]
    [InlineData("--file", "in.json")]
    [InlineData("--yes")]
    [InlineData("--json")]
    [InlineData("--dry-run")]
    public async Task ImportOptions_AreNotAcceptedByExport(params string[] option)
    {
        int exitCode = await ExportAsync(["-o", "-", .. option]);

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains(option[0], cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }

    [Theory]
    [InlineData("--output", "out.json")]
    [InlineData("--flat")]
    [InlineData("--force")]
    public async Task ExportOptions_AreNotAcceptedByImport(params string[] option)
    {
        cli.Service.Returns(TestData.Initialized());

        int exitCode = await cli.RunImportAsync(["--dry-run", .. option]);

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains(option[0], cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task RootImport_StillRuns_WithAFileNamedExport()
    {
        cli.Service.Returns(TestData.Initialized());

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "export", "--dry-run");

        // "export" is the value of --file, not the subcommand: the import flow runs and fails to find the file.
        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("JSON file not found", cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }

    [Fact]
    public async Task RootHelp_ListsTheExportCommand()
    {
        int exitCode = await cli.RunAsync("--help");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Commands:", cli.Out.Output);
        Assert.Contains("export", cli.Out.Output);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task ExportHelp_DescribesEveryOption(string flag)
    {
        int exitCode = await cli.RunAsync("export", flag);

        Assert.Equal(ExitCodes.Success, exitCode);
        string help = cli.Out.Output;
        Assert.Contains("secret-import export", help);
        foreach (string option in new[] { "--project", "--output", "--section", "--flat", "--force" })
        {
            Assert.Contains(option, help);
        }

        Assert.Contains("file|-", help);
        Assert.Contains("plain-text", help);
        Assert.Contains("stdout", help);
        Assert.DoesNotContain("--file", help);
        Assert.DoesNotContain("--mode", help);
        Assert.Empty(cli.Err.Output);
        Assert.Empty(cli.Service.ExportRequests);
    }
}
