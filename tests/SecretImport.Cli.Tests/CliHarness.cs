using Spectre.Console.Testing;

using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary>A <see cref="CliApp"/> over two <see cref="TestConsole"/>s (stdout / stderr), a fake stdin and a temp JSON file.</summary>
internal sealed class CliHarness : IDisposable
{
    public CliHarness(string stdin = "")
    {
        Out = new TestConsole().Width(400);
        Err = new TestConsole().Width(400);
        Stdin = new StringReader(stdin);
        JsonPath = Path.Combine(Path.GetTempPath(), $"secret-import-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(JsonPath, """{ "Api": { "Key": "x" } }""");
    }

    public TestConsole Out { get; }

    public TestConsole Err { get; }

    public TextReader Stdin { get; set; }

    public FakeImportService Service { get; } = new();

    public string JsonPath { get; }

    /// <summary>The resolver handed to <see cref="CliApp"/>; null lets it use Core's default over the real file system.</summary>
    public SolutionScopeResolver? ScopeResolver { get; set; }

    public CliHarness Interactive(params string[] answers)
    {
        Out.Interactive();
        foreach (string answer in answers)
        {
            Out.Input.PushTextWithEnter(answer);
        }

        return this;
    }

    public Task<int> RunAsync(params string[] args)
    {
        return new CliApp(Out, Err, Stdin, Service, ScopeResolver).RunAsync(args, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs with <c>--project</c> and <c>--file</c> pointing at the temp JSON file, plus <paramref name="extra"/>.</summary>
    public Task<int> RunImportAsync(params string[] extra)
    {
        return RunAsync(["--project", TestData.Project.Path, "--file", JsonPath, .. extra]);
    }

    public void Dispose()
    {
        File.Delete(JsonPath);
        Out.Dispose();
        Err.Dispose();
    }
}
