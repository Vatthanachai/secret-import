using System.Text.Json;

using Spectre.Console.Testing;

using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary><c>--json</c>: exactly one JSON object on stdout for every outcome, nothing on stderr, never a prompt (ADR-0014).</summary>
public sealed class JsonOutputTests : IDisposable
{
    private readonly CliHarness cli = new();

    public void Dispose() => cli.Dispose();

    /// <summary>Parses stdout as a single JSON document (trailing content fails) and checks the process exit code matches <c>exitCode</c>.</summary>
    private JsonElement Report(int processExitCode)
    {
        Assert.Empty(cli.Err.Output);
        using JsonDocument document = JsonDocument.Parse(cli.Out.Output);
        JsonElement root = document.RootElement.Clone();
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(processExitCode, root.GetProperty("exitCode").GetInt32());
        return root;
    }

    private static void AssertNull(JsonElement element, string property)
    {
        Assert.Equal(JsonValueKind.Null, element.GetProperty(property).ValueKind);
    }

    // ---- dryRun ----

    [Fact]
    public async Task DryRun_WritesTheWholeReport_WithValuesOmitted()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run", "--json");

        Assert.Equal(ExitCodes.Success, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("dryRun", report.GetProperty("status").GetString());
        Assert.Equal(TestData.Project.Name, report.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal(TestData.Project.Path, report.GetProperty("project").GetProperty("path").GetString());
        Assert.Equal("app-secrets-id", report.GetProperty("userSecretsId").GetString());
        Assert.Equal("/secrets/app-secrets-id/secrets.json", report.GetProperty("secretsPath").GetString());
        Assert.Equal("overwrite", report.GetProperty("mode").GetString());
        AssertNull(report, "section");
        Assert.False(report.GetProperty("initialized").GetBoolean());
        JsonElement summary = report.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("added").GetInt32());
        Assert.Equal(1, summary.GetProperty("changed").GetInt32());
        Assert.Equal(0, summary.GetProperty("removed").GetInt32());
        Assert.Equal(1, summary.GetProperty("unchanged").GetInt32());
        Assert.Equal(0, summary.GetProperty("skipped").GetInt32());
        JsonElement[] changes = [.. report.GetProperty("changes").EnumerateArray()];
        Assert.Equal(["Api:Key", "Db:Password", "Same"], changes.Select(static c => c.GetProperty("key").GetString()));
        Assert.Equal(["added", "changed", "unchanged"], changes.Select(static c => c.GetProperty("kind").GetString()));
        Assert.All(changes, static c => Assert.False(c.TryGetProperty("oldValue", out _)));
        Assert.All(changes, static c => Assert.False(c.TryGetProperty("newValue", out _)));
        Assert.Empty(report.GetProperty("warnings").EnumerateArray());
        AssertNull(report, "backupPath");
        AssertNull(report, "error");
        Assert.DoesNotContain(TestData.SecretValue, cli.Out.Output);
        Assert.DoesNotContain(TestData.OldSecretValue, cli.Out.Output);
        Assert.DoesNotContain(AnalysisRenderer.Mask, cli.Out.Output);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task DryRun_ExitCode_WithChanges_ReportsAndReturnsFour()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run", "--exit-code", "--json");

        Assert.Equal(ExitCodes.ChangesPending, exitCode);
        Assert.Equal("dryRun", Report(exitCode).GetProperty("status").GetString());
    }

    [Fact]
    public async Task DryRun_ExitCode_WithoutChanges_ReturnsZero()
    {
        cli.Service.Returns(TestData.Initialized(new SecretChange("Same", SecretChangeKind.Unchanged, "a", "a")));

        int exitCode = await cli.RunImportAsync("--dry-run", "--exit-code", "--json");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("dryRun", Report(exitCode).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ShowValues_IncludesOldAndNewValues_NullWhenAbsent()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run", "--json", "--show-values");

        JsonElement[] changes = [.. Report(exitCode).GetProperty("changes").EnumerateArray()];
        AssertNull(changes[0], "oldValue");
        Assert.Equal(TestData.SecretValue, changes[0].GetProperty("newValue").GetString());
        Assert.Equal(TestData.OldSecretValue, changes[1].GetProperty("oldValue").GetString());
        Assert.Equal(TestData.SecretValue, changes[1].GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task SectionScopeModeAndWarnings_AreReported()
    {
        ImportWarning warning = new(ImportWarningKind.SharedSecretsId, "The UserSecretsId is shared with Other.");
        cli.Service.Returns(TestData.Analysis(TestData.SomeChanges(), warnings: [warning]));

        int exitCode = await cli.RunImportAsync("--dry-run", "--json", "--mode", "replace", "--section", "Db", "--scope", "all/All.slnx");

        JsonElement report = Report(exitCode);
        Assert.Equal("replace", report.GetProperty("mode").GetString());
        Assert.Equal("Db", report.GetProperty("section").GetString());
        Assert.Equal("all/All.slnx", report.GetProperty("scope").GetString());
        JsonElement reported = Assert.Single(report.GetProperty("warnings").EnumerateArray());
        Assert.Equal("sharedSecretsId", reported.GetProperty("kind").GetString());
        Assert.Equal(warning.Message, reported.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Json_IsWrittenRaw_NeverWrappedByTheConsoleWidth()
    {
        string longKey = "Very:Long:" + new string('k', 200);
        cli.Out.Width(20);
        cli.Service.Returns(TestData.Initialized(new SecretChange(longKey, SecretChangeKind.Added, null, "v")));

        int exitCode = await cli.RunImportAsync("--dry-run", "--json");

        JsonElement change = Assert.Single(Report(exitCode).GetProperty("changes").EnumerateArray());
        Assert.Equal(longKey, change.GetProperty("key").GetString());
    }

    // ---- noChanges ----

    [Fact]
    public async Task NoChanges_ReportsNoChanges_AndReturnsZero()
    {
        cli.Service.Returns(TestData.Initialized(new SecretChange("Same", SecretChangeKind.Unchanged, "a", "a")));

        int exitCode = await cli.RunImportAsync("--json");

        Assert.Equal(ExitCodes.Success, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("noChanges", report.GetProperty("status").GetString());
        Assert.Equal(1, report.GetProperty("summary").GetProperty("unchanged").GetInt32());
        Assert.Empty(cli.Service.ApplyCalls);
    }

    // ---- applied ----

    [Fact]
    public async Task Yes_Applies_AndReportsTheBackup()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyResult = new ImportResult(1, 1, 0, "/secrets/app-secrets-id/secrets.json", "/secrets/app-secrets-id/secrets.json.bak");

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.Success, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("applied", report.GetProperty("status").GetString());
        Assert.Equal("/secrets/app-secrets-id/secrets.json", report.GetProperty("secretsPath").GetString());
        Assert.Equal("/secrets/app-secrets-id/secrets.json.bak", report.GetProperty("backupPath").GetString());
        Assert.False(report.GetProperty("initialized").GetBoolean());
        AssertNull(report, "error");
        Assert.Single(cli.Service.ApplyCalls);
        Assert.DoesNotContain(TestData.SecretValue, cli.Out.Output);
    }

    [Fact]
    public async Task InitAndYes_InitializesAppliesAndReportsInitialized()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()), TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--json", "--yes", "--init");

        Assert.Equal(ExitCodes.Success, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("applied", report.GetProperty("status").GetString());
        Assert.True(report.GetProperty("initialized").GetBoolean());
        Assert.Equal("app-secrets-id", report.GetProperty("userSecretsId").GetString());
        Assert.Single(cli.Service.InitCalls);
        Assert.Single(cli.Service.ApplyCalls);
    }

    // ---- cancelled ----

    [Fact]
    public async Task WithoutYes_NeverPrompts_EvenWhenInteractive_AndReportsConfirmationRequired()
    {
        cli.Interactive("y").Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--json");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("cancelled", report.GetProperty("status").GetString());
        JsonElement error = report.GetProperty("error");
        Assert.Equal("confirmationRequired", error.GetProperty("kind").GetString());
        Assert.Contains("--yes", error.GetProperty("message").GetString());
        Assert.Contains("--json", error.GetProperty("message").GetString());
        Assert.Empty(error.GetProperty("candidates").EnumerateArray());
        Assert.Equal(TestData.Project.Name, report.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal(3, report.GetProperty("changes").GetArrayLength());
        Assert.Empty(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task Uninitialized_WithoutInit_ReportsConfirmationRequired_NamingInit()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        JsonElement report = Report(exitCode);
        AssertNull(report, "userSecretsId");
        AssertNull(report, "secretsPath");
        Assert.Contains("--init", report.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(cli.Service.InitCalls);
    }

    [Fact]
    public async Task Stdin_WithoutYes_ReportsConfirmationRequired_BeforeAnalyze()
    {
        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--json");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("cancelled", report.GetProperty("status").GetString());
        Assert.Equal("confirmationRequired", report.GetProperty("error").GetProperty("kind").GetString());
        AssertNull(report, "project");
        AssertNull(report, "mode");
        AssertNull(report, "summary");
        Assert.Empty(report.GetProperty("changes").EnumerateArray());
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task Cancellation_ReportsCancelled()
    {
        cli.Service.AnalyzeException = new OperationCanceledException();

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("cancelled", report.GetProperty("status").GetString());
        Assert.Equal("cancelled", report.GetProperty("error").GetProperty("kind").GetString());
    }

    // ---- error ----

    [Fact]
    public async Task ExitCodeWithoutDryRun_ReportsInvalidOptions_AndReturnsOne()
    {
        int exitCode = await cli.RunImportAsync("--json", "--exit-code", "--yes");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("error", report.GetProperty("status").GetString());
        Assert.Equal("invalidOptions", report.GetProperty("error").GetProperty("kind").GetString());
        Assert.Contains("--dry-run", report.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task MultipleProjects_ReportsTheCandidates()
    {
        ProjectRef a = new("/repo/A/A.csproj", "A");
        ProjectRef b = new("/repo/B/B.fsproj", "B");
        cli.Service.AnalyzeException = new ImportException(ImportErrorKind.MultipleProjects, "'/repo' contains 2 projects.", [a, b]);

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("error", report.GetProperty("status").GetString());
        AssertNull(report, "project");
        JsonElement error = report.GetProperty("error");
        Assert.Equal("multipleProjects", error.GetProperty("kind").GetString());
        Assert.Equal("'/repo' contains 2 projects.", error.GetProperty("message").GetString());
        JsonElement[] candidates = [.. error.GetProperty("candidates").EnumerateArray()];
        Assert.Equal(["A", "B"], candidates.Select(static c => c.GetProperty("name").GetString()));
        Assert.Equal([a.Path, b.Path], candidates.Select(static c => c.GetProperty("path").GetString()));
    }

    [Theory]
    [InlineData(ImportErrorKind.InvalidInput, "invalidInput", ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.NoProjectFound, "noProjectFound", ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.SdkUnavailable, "sdkUnavailable", ExitCodes.EnvironmentError)]
    [InlineData(ImportErrorKind.IoFailure, "ioFailure", ExitCodes.EnvironmentError)]
    public async Task AnalyzeErrors_ReportTheirKind(ImportErrorKind kind, string expectedKind, int expectedExitCode)
    {
        cli.Service.AnalyzeException = new ImportException(kind, $"boom {kind}");

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(expectedExitCode, exitCode);
        JsonElement error = Report(exitCode).GetProperty("error");
        Assert.Equal(expectedKind, error.GetProperty("kind").GetString());
        Assert.Equal($"boom {kind}", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ConcurrentModificationOnApply_KeepsTheAnalysisInTheReport()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyException = new ImportException(ImportErrorKind.ConcurrentModification, "secrets.json changed; run again.");

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("concurrentModification", report.GetProperty("error").GetProperty("kind").GetString());
        Assert.Equal(TestData.Project.Path, report.GetProperty("project").GetProperty("path").GetString());
        AssertNull(report, "backupPath");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task IoExceptions_ReportIoFailure_AndReturnThree(Type exceptionType)
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyException = (Exception)Activator.CreateInstance(exceptionType, "Disk on fire.")!;

        int exitCode = await cli.RunImportAsync("--json", "--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("error", report.GetProperty("status").GetString());
        Assert.Equal("ioFailure", report.GetProperty("error").GetProperty("kind").GetString());
        Assert.Equal("Disk on fire.", report.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task InitWithoutSdk_ReportsSdkUnavailable()
    {
        cli.Service.Returns(TestData.NotInitialized(false, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--json", "--yes", "--init");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        JsonElement report = Report(exitCode);
        Assert.Equal("sdkUnavailable", report.GetProperty("error").GetProperty("kind").GetString());
        Assert.Empty(cli.Service.InitCalls);
    }

    [Fact]
    public async Task InitFailure_AfterNothingWasInitialized_ReportsInitializedFalse()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));
        cli.Service.InitException = new ImportException(ImportErrorKind.SdkUnavailable, "The .NET SDK was not found.");

        int exitCode = await cli.RunImportAsync("--json", "--yes", "--init");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.False(Report(exitCode).GetProperty("initialized").GetBoolean());
    }

    [Fact]
    public async Task ParseErrors_StayTextOnStderr()
    {
        int exitCode = await cli.RunImportAsync("--json", "--no-such-option");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--no-such-option", cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }
}