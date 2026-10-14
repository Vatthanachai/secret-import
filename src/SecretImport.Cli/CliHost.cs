using Microsoft.Extensions.DependencyInjection;

using Spectre.Console;

using SecretImport.Core;

namespace SecretImport.Cli;

/// <summary>Composition root of the CLI. <c>Program.cs</c> calls <see cref="CreateDefault"/> and nothing else.</summary>
public static class CliHost
{
    /// <summary>Wires the real process console and Core's <see cref="IImportService"/>.</summary>
    public static CliApp CreateDefault()
    {
        return new ServiceCollection()
            .AddSecretImportCli(Console.Out, Console.Error, Console.In, Console.IsInputRedirected)
            .AddSecretImportCore()
            .BuildServiceProvider(validateScopes: true)
            .GetRequiredService<CliApp>();
    }

    /// <summary>Registers <see cref="CliApp"/> over the given streams. <see cref="IImportService"/> must be registered separately.</summary>
    public static IServiceCollection AddSecretImportCli(
        this IServiceCollection services, TextWriter stdout, TextWriter stderr, TextReader stdin, bool inputRedirected)
    {
        services.AddSingleton(sp => new CliApp(
            CreateConsole(stdout, interactive: !inputRedirected),
            CreateConsole(stderr, interactive: false),
            stdin,
            sp.GetRequiredService<IImportService>()));
        return services;
    }

    /// <summary>A console that never prompts when <paramref name="interactive"/> is false (stdin redirected, ADR-0005).</summary>
    public static IAnsiConsole CreateConsole(TextWriter writer, bool interactive)
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Interactive = interactive ? InteractionSupport.Detect : InteractionSupport.No,
        });
    }
}
