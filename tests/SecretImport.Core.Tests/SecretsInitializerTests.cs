namespace SecretImport.Core.Tests;

public sealed class SecretsInitializerTests
{
    private static readonly ProjectRef Project = new(Path.Combine(Path.GetTempPath(), "app", "App.csproj"), "App");

    [Fact]
    public async Task InitAsync_RunsUserSecretsInit_ForTheProject()
    {
        FakeDotnetCli cli = new();

        await new SecretsInitializer(cli).InitAsync(Project, TestContext.Current.CancellationToken);

        (IReadOnlyList<string> args, string? cwd) = Assert.Single(cli.Calls);
        Assert.Equal(["user-secrets", "init", "--project", Project.Path], args);
        Assert.Equal(Path.GetDirectoryName(Project.Path), cwd);
    }

    [Fact]
    public async Task InitAsync_WithoutSdk_IsSdkUnavailable_AndTellsTheUserWhatToRun()
    {
        FakeDotnetCli cli = FakeDotnetCli.Unavailable();

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => new SecretsInitializer(cli).InitAsync(Project, TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.SdkUnavailable, ex.Kind);
        Assert.Contains($"dotnet user-secrets init --project \"{Project.Path}\"", ex.Message, StringComparison.Ordinal);
        Assert.Empty(cli.Calls);
    }

    [Fact]
    public async Task InitAsync_WhenTheCommandFails_IsIoFailure_WithTheToolOutput()
    {
        FakeDotnetCli cli = new() { Handler = static _ => new DotnetResult(1, "out text", "Could not find project") };

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => new SecretsInitializer(cli).InitAsync(Project, TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.Contains("Could not find project", ex.Message, StringComparison.Ordinal);
        Assert.Contains("exit code 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitAsync_WhenTheCommandFailsSilently_FallsBackToStdout()
    {
        FakeDotnetCli cli = new() { Handler = static _ => new DotnetResult(2, "only stdout", "  ") };

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => new SecretsInitializer(cli).InitAsync(Project, TestContext.Current.CancellationToken));

        Assert.Contains("only stdout", ex.Message, StringComparison.Ordinal);
    }
}