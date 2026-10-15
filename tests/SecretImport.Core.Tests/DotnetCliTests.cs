namespace SecretImport.Core.Tests;

public sealed class DotnetCliTests
{
    private static readonly DotnetCli Missing = new("secret-import-no-such-executable-7f3a", TimeSpan.FromSeconds(10));

    [Fact]
    public async Task RunAsync_WhenExecutableIsMissing_IsSdkUnavailable()
    {
        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => Missing.RunAsync(["--version"], ct: TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.SdkUnavailable, ex.Kind);
    }

    [Fact]
    public async Task IsSdkAvailableAsync_WhenExecutableIsMissing_IsFalse()
    {
        Assert.False(await Missing.IsSdkAvailableAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WhenAlreadyCancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DotnetCli().RunAsync(["--version"], ct: cts.Token));
    }

    [Fact]
    public void DefaultConstructor_UsesDotnetAndATwoMinuteTimeout()
    {
        DotnetCli cli = new();

        Assert.Equal("dotnet", cli.Executable);
        Assert.Equal(TimeSpan.FromMinutes(2), cli.Timeout);
    }

    [Fact]
    public void BuildStartInfo_RemovesInheritedMsBuildVariables_AndSilencesTheCli()
    {
        DotnetCli cli = new();
        string? previous = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", "inherited-from-test-host");
        try
        {
            System.Diagnostics.ProcessStartInfo info = cli.BuildStartInfo(["msbuild", "a b.csproj"], "work-dir");

            Assert.Equal("dotnet", info.FileName);
            Assert.Equal(["msbuild", "a b.csproj"], info.ArgumentList);
            Assert.Equal("work-dir", info.WorkingDirectory);
            Assert.DoesNotContain(info.Environment.Keys, static k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("1", info.Environment["DOTNET_NOLOGO"]);
            Assert.Equal("1", info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
            Assert.True(info.RedirectStandardOutput);
            Assert.True(info.RedirectStandardError);
            Assert.False(info.UseShellExecute);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", previous);
        }
    }

    [Fact]
    public void DotnetResult_Succeeded_IsExitCodeZero()
    {
        Assert.True(new DotnetResult(0, "", "").Succeeded);
        Assert.False(new DotnetResult(1, "", "").Succeeded);
    }
}

[Trait("Category", "Integration")]
public sealed class DotnetCliIntegrationTests
{
    [Fact]
    public async Task RunAsync_CapturesStdoutAndExitCode()
    {
        DotnetResult result = await new DotnetCli().RunAsync(["--version"], ct: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task RunAsync_CapturesNonZeroExitCode()
    {
        string dir = Directory.CreateTempSubdirectory("secret-import-cli-").FullName;
        try
        {
            DotnetResult result = await new DotnetCli().RunAsync(["msbuild", Path.Combine(dir, "missing.csproj"), "-getProperty:UserSecretsId"], dir, TestContext.Current.CancellationToken);

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput + result.StandardError));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task IsSdkAvailableAsync_IsTrue_AndCached()
    {
        DotnetCli cli = new();

        Assert.True(await cli.IsSdkAvailableAsync(TestContext.Current.CancellationToken));
        Assert.True(await cli.IsSdkAvailableAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WhenTimeoutElapses_IsSdkUnavailable()
    {
        DotnetCli cli = new("dotnet", TimeSpan.FromMilliseconds(1));

        ImportException ex = await Assert.ThrowsAsync<ImportException>(() => cli.RunAsync(["--info"], ct: TestContext.Current.CancellationToken));

        Assert.Equal(ImportErrorKind.SdkUnavailable, ex.Kind);
        Assert.Contains("timed out", ex.Message, StringComparison.Ordinal);
    }
}