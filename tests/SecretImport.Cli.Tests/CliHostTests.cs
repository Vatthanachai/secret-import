using Microsoft.Extensions.DependencyInjection;

using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli.Tests;

public class CliHostTests
{
    [Fact]
    public async Task AddSecretImportCli_ResolvesCliApp_WritingOutputToStdoutAndErrorsToStderr()
    {
        StringWriter stdout = new();
        StringWriter stderr = new();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IImportService>(new FakeImportService())
            .AddSecretImportCli(stdout, stderr, new StringReader(""), inputRedirected: true)
            .BuildServiceProvider(validateScopes: true);
        CliApp app = provider.GetRequiredService<CliApp>();

        int help = await app.RunAsync(["--help"], TestContext.Current.CancellationToken);
        int bad = await app.RunAsync(["--bogus"], TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, help);
        Assert.Equal(ExitCodes.InvalidInput, bad);
        Assert.Contains("Usage:", stdout.ToString());
        Assert.DoesNotContain("--bogus", stdout.ToString());
        Assert.Contains("--bogus", stderr.ToString());
    }

    [Fact]
    public void AddSecretImportCli_RequiresAnImportService()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSecretImportCli(new StringWriter(), new StringWriter(), new StringReader(""), inputRedirected: false)
            .BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<CliApp>());
    }

    [Fact]
    public void CreateConsole_NonInteractive_NeverPrompts()
    {
        IAnsiConsole console = CliHost.CreateConsole(new StringWriter(), interactive: false);

        Assert.False(console.Profile.Capabilities.Interactive);
    }

    [Fact]
    public void CreateConsole_WritesToTheGivenWriter()
    {
        StringWriter writer = new();
        IAnsiConsole console = CliHost.CreateConsole(writer, interactive: true);

        console.WriteLine("hello");

        Assert.Contains("hello", writer.ToString());
    }
}
