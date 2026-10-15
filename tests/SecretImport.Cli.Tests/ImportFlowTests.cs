using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary>Analyze → init → confirm → Apply through the CLI, and the exit codes of every outcome (ADR-0005, ADR-0008).</summary>
public sealed class ImportFlowTests : IDisposable
{
    private readonly CliHarness cli = new();

    public void Dispose() => cli.Dispose();

    private string AllOutput => cli.Out.Output + cli.Err.Output;

    // ---- --dry-run ----

    [Fact]
    public async Task DryRun_ShowsTheDiff_WritesNothing_AndReturnsZero()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Api:Key", cli.Out.Output);
        Assert.Contains("Dry run", cli.Out.Output);
        Assert.Empty(cli.Service.ApplyCalls);
        Assert.Empty(cli.Service.InitCalls);
        Assert.Empty(cli.Err.Output);
    }

    [Fact]
    public async Task DryRun_OnAnUninitializedProject_NeverInitializes_AndReturnsZero()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run", "--init", "--yes");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(cli.Service.InitCalls);
        Assert.Empty(cli.Service.ApplyCalls);
        Assert.Contains("(not initialized)", cli.Out.Output);
    }

    [Fact]
    public async Task DryRun_WithStdin_DoesNotNeedYes()
    {
        cli.Stdin = new StringReader("{}");
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--dry-run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    // ---- --exit-code (ADR-0014) ----

    [Fact]
    public async Task DryRun_ExitCode_WithChanges_ShowsTheDiff_AndReturnsFour()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--dry-run", "--exit-code");

        Assert.Equal(ExitCodes.ChangesPending, exitCode);
        Assert.Contains("Api:Key", cli.Out.Output);
        Assert.Contains("Dry run", cli.Out.Output);
        Assert.Empty(cli.Service.ApplyCalls);
        Assert.Empty(cli.Err.Output);
    }

    [Fact]
    public async Task DryRun_ExitCode_WithoutChanges_ReturnsZero()
    {
        cli.Service.Returns(TestData.Initialized(new SecretChange("Same", SecretChangeKind.Unchanged, "a", "a")));

        int exitCode = await cli.RunImportAsync("--dry-run", "--exit-code");

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task ExitCode_WithoutDryRun_ReturnsOne_WithoutReadingOrWritingAnything()
    {
        cli.Stdin = new ThrowingReader();
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--exit-code", "--yes");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--exit-code", cli.Err.Output);
        Assert.Contains("--dry-run", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    // ---- masking ----

    [Fact]
    public async Task SecretValues_AreMaskedByDefault()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        await cli.RunImportAsync("--yes");

        Assert.DoesNotContain(TestData.SecretValue, AllOutput);
        Assert.DoesNotContain(TestData.OldSecretValue, AllOutput);
    }

    [Fact]
    public async Task ShowValues_RevealsSecretValues()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        await cli.RunImportAsync("--dry-run", "--show-values");

        Assert.Contains(TestData.SecretValue, cli.Out.Output);
        Assert.Contains(TestData.OldSecretValue, cli.Out.Output);
    }

    // ---- no changes ----

    [Fact]
    public async Task NoChanges_WritesNothing_AndReturnsZero_EvenWithoutYes()
    {
        cli.Service.Returns(TestData.Initialized(new SecretChange("Same", SecretChangeKind.Unchanged, "a", "a")));

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("No changes", cli.Out.Output);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task NoChanges_OnAnUninitializedProject_DoesNotInitialize()
    {
        cli.Service.Returns(TestData.NotInitialized(true));

        int exitCode = await cli.RunImportAsync("--init", "--yes");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Empty(cli.Service.InitCalls);
    }

    // ---- confirm ----

    [Fact]
    public async Task Yes_AppliesWithoutPrompting_AndPrintsTheResult()
    {
        ImportAnalysis analysis = TestData.Initialized(TestData.SomeChanges());
        cli.Service.Returns(analysis);

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.Success, exitCode);
        ImportAnalysis applied = Assert.Single(cli.Service.ApplyCalls);
        Assert.Same(analysis.Plan, applied.Plan);
        Assert.Contains("+1 added, ~2 changed, -3 removed", cli.Out.Output);
        Assert.Contains("/secrets/secrets.json", cli.Out.Output);
        Assert.Contains("Backup: /secrets/secrets.json.bak", cli.Out.Output);
    }

    [Fact]
    public async Task Result_WithoutBackup_OmitsTheBackupLine()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyResult = new ImportResult(1, 0, 0, "/secrets/secrets.json", null);

        await cli.RunImportAsync("--yes");

        Assert.DoesNotContain("Backup:", cli.Out.Output);
    }

    [Fact]
    public async Task Interactive_ConfirmYes_Applies()
    {
        cli.Interactive("y").Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Single(cli.Service.ApplyCalls);
        Assert.Contains("Import into /secrets/app-secrets-id/secrets.json?", cli.Out.Output);
    }

    [Fact]
    public async Task Interactive_ConfirmNo_CancelsWithTwo()
    {
        cli.Interactive("n").Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(cli.Service.ApplyCalls);
        Assert.Contains("Cancelled", cli.Err.Output);
    }

    [Fact]
    public async Task NonInteractive_WithoutYes_ReturnsTwo_WithoutPrompting()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(cli.Service.ApplyCalls);
        Assert.Contains("not interactive", cli.Err.Output);
        Assert.Contains("--yes", cli.Err.Output);
        Assert.DoesNotContain("--init", cli.Err.Output);
    }

    // ---- stdin ----

    [Fact]
    public async Task Stdin_WithoutYes_ReturnsTwo_BeforeReadingStdin()
    {
        cli.Stdin = new ThrowingReader();
        cli.Interactive("y").Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("--yes", cli.Err.Output);
        Assert.Contains("--dry-run", cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task Stdin_WithYes_OnAnUninitializedProject_NeedsInit_EvenWhenInteractive()
    {
        cli.Stdin = new StringReader("{}");
        cli.Interactive("y", "y").Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--yes");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("stdin", cli.Err.Output);
        Assert.Contains("--init", cli.Err.Output);
        Assert.Empty(cli.Service.InitCalls);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task Stdin_WithYesAndInit_InitializesAndApplies()
    {
        cli.Stdin = new StringReader("{}");
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()), TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--yes", "--init");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(TestData.Project, Assert.Single(cli.Service.InitCalls));
        Assert.Single(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task Stdin_ReadFailure_ReturnsThree()
    {
        cli.Stdin = new ThrowingReader();

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--dry-run");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("stdin broke", cli.Err.Output);
    }

    // ---- init ----

    [Fact]
    public async Task Init_WithoutSdk_ReturnsThree_AndTellsTheUserWhatToRun()
    {
        cli.Service.Returns(TestData.NotInitialized(false, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--init", "--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains($"dotnet user-secrets init --project \"{TestData.Project.Path}\"", cli.Err.Output);
        Assert.Empty(cli.Service.InitCalls);
    }

    [Fact]
    public async Task Interactive_InitYes_ThenConfirmYes_InitializesReanalyzesAndApplies()
    {
        ImportAnalysis afterInit = TestData.Initialized(TestData.SomeChanges());
        cli.Interactive("y", "y").Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()), afterInit);

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Single(cli.Service.InitCalls);
        Assert.Equal(2, cli.Service.AnalyzeRequests.Count);
        Assert.Equal("app-secrets-id", Assert.Single(cli.Service.ApplyCalls).Project.UserSecretsId);
        Assert.Contains("has no UserSecretsId", cli.Out.Output);
        Assert.Contains("Initialized user secrets", cli.Out.Output);
    }

    [Fact]
    public async Task Interactive_InitNo_CancelsWithTwo()
    {
        cli.Interactive("n").Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(cli.Service.InitCalls);
        Assert.Contains("not initialized", cli.Err.Output);
    }

    [Fact]
    public async Task InitFlag_SkipsTheInitPrompt_ButStillConfirms()
    {
        cli.Interactive("y").Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()), TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--init");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Single(cli.Service.InitCalls);
        Assert.DoesNotContain("has no UserSecretsId", cli.Out.Output);
        Assert.Contains("Import into", cli.Out.Output);
    }

    [Fact]
    public async Task NonInteractive_InitWithoutYes_ReturnsTwo_BeforeInitializing()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync("--init");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Empty(cli.Service.InitCalls);
        Assert.Contains("Pass --yes to continue", cli.Err.Output);
    }

    [Fact]
    public async Task NonInteractive_UninitializedWithoutFlags_AsksForBothFlags()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));

        int exitCode = await cli.RunImportAsync();

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("Pass --init and --yes to continue", cli.Err.Output);
    }

    [Fact]
    public async Task InitFailure_ReturnsThree()
    {
        cli.Service.Returns(TestData.NotInitialized(true, TestData.SomeChanges()));
        cli.Service.InitException = new ImportException(ImportErrorKind.SdkUnavailable, "The .NET SDK was not found.");

        int exitCode = await cli.RunImportAsync("--init", "--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("The .NET SDK was not found.", cli.Err.Output);
        Assert.Empty(cli.Service.ApplyCalls);
    }

    // ---- errors ----

    [Fact]
    public async Task MultipleProjects_ReturnsOne_AndListsTheCandidatesOnStderr()
    {
        ProjectRef a = new("/repo/A/A.csproj", "A");
        ProjectRef b = new("/repo/B/B.fsproj", "B");
        cli.Service.AnalyzeException = new ImportException(ImportErrorKind.MultipleProjects, "'/repo' contains 2 projects.", [a, b]);

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("'/repo' contains 2 projects.", cli.Err.Output);
        Assert.Contains("/repo/A/A.csproj", cli.Err.Output);
        Assert.Contains("/repo/B/B.fsproj", cli.Err.Output);
        Assert.Contains("--project", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Theory]
    [InlineData(ImportErrorKind.InvalidInput, ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.NoProjectFound, ExitCodes.InvalidInput)]
    [InlineData(ImportErrorKind.SdkUnavailable, ExitCodes.EnvironmentError)]
    [InlineData(ImportErrorKind.IoFailure, ExitCodes.EnvironmentError)]
    public async Task AnalyzeErrors_MapToTheirExitCode_AndGoToStderr(ImportErrorKind kind, int expected)
    {
        cli.Service.AnalyzeException = new ImportException(kind, $"boom {kind}");

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(expected, exitCode);
        Assert.Contains($"error: boom {kind}", cli.Err.Output);
        Assert.DoesNotContain("--project", cli.Err.Output);
        Assert.Empty(cli.Out.Output);
    }

    [Fact]
    public async Task ConcurrentModificationOnApply_ReturnsThree()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyException = new ImportException(ImportErrorKind.ConcurrentModification, "secrets.json changed; run again.");

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("secrets.json changed; run again.", cli.Err.Output);
    }

    [Fact]
    public async Task MissingJsonFile_ReturnsOne()
    {
        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("JSON file not found", cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task Cancellation_ReturnsTwo()
    {
        cli.Service.AnalyzeException = new OperationCanceledException();

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.Cancelled, exitCode);
        Assert.Contains("Cancelled.", cli.Err.Output);
    }

    [Fact]
    public async Task IoFailureOutsideCore_ReturnsThree()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));
        cli.Service.ApplyException = new UnauthorizedAccessException("Access denied.");

        int exitCode = await cli.RunImportAsync("--yes");

        Assert.Equal(ExitCodes.EnvironmentError, exitCode);
        Assert.Contains("Access denied.", cli.Err.Output);
    }

    private sealed class ThrowingReader : TextReader
    {
        public override Task<string> ReadToEndAsync(CancellationToken cancellationToken) => throw new IOException("stdin broke");
    }
}
