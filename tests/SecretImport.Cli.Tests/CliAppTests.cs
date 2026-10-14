using Spectre.Console.Testing;

namespace SecretImport.Cli.Tests;

public class CliAppTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_PrintsUsageToTheConsole_AndReturnsZero(string flag)
    {
        TestConsole console = new();

        int exitCode = await new CliApp(console).RunAsync([flag], TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("secret-import", console.Output);
        Assert.Contains("Usage:", console.Output);
        Assert.Contains("user-secrets", console.Output);
    }

    [Fact]
    public async Task UnknownOption_ReportsAnError_AndReturnsOne()
    {
        TestConsole console = new();

        int exitCode = await new CliApp(console).RunAsync(["--no-such-option"], TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("--no-such-option", console.Output);
    }

    [Fact]
    public async Task Version_PrintsTheVersion_AndReturnsZero()
    {
        TestConsole console = new();

        int exitCode = await new CliApp(console).RunAsync(["--version"], TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", console.Output.Trim());
    }
}