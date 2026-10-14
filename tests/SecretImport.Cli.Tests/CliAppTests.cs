using SecretImport.Core;

namespace SecretImport.Cli.Tests;

/// <summary>Argument parsing and the help / version / parse-error paths of <see cref="CliApp"/> (ADR-0008).</summary>
public sealed class CliAppTests : IDisposable
{
    private readonly CliHarness cli = new();

    public void Dispose() => cli.Dispose();

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_PrintsUsageAndEveryOptionToStdout_AndReturnsZero(string flag)
    {
        int exitCode = await cli.RunAsync(flag);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("secret-import", cli.Out.Output);
        Assert.Contains("Usage:", cli.Out.Output);
        Assert.Contains("user-secrets", cli.Out.Output);
        foreach (string option in new[] { "--project", "--file", "--mode", "--section", "--dry-run", "--yes", "--init", "--show-values" })
        {
            Assert.Contains(option, cli.Out.Output);
        }

        Assert.Contains("overwrite|skip|replace", cli.Out.Output);
        Assert.Empty(cli.Err.Output);
    }

    [Fact]
    public async Task Version_PrintsTheVersion_AndReturnsZero()
    {
        int exitCode = await cli.RunAsync("--version");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", cli.Out.Output.Trim());
    }

    [Fact]
    public async Task UnknownOption_ReportsTheErrorOnStderr_AndReturnsOne()
    {
        int exitCode = await cli.RunImportAsync("--no-such-option");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("--no-such-option", cli.Err.Output);
        Assert.DoesNotContain("--no-such-option", cli.Out.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Theory]
    [InlineData("--file", "x.json")]
    [InlineData("--project", "App.csproj")]
    public async Task MissingRequiredOption_ReturnsOne_WithoutCallingCore(string option, string value)
    {
        int exitCode = await cli.RunAsync(option, value);

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.NotEmpty(cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Fact]
    public async Task InvalidMode_ReturnsOne()
    {
        int exitCode = await cli.RunImportAsync("--mode", "merge");

        Assert.Equal(ExitCodes.InvalidInput, exitCode);
        Assert.Contains("merge", cli.Err.Output);
        Assert.Empty(cli.Service.AnalyzeRequests);
    }

    [Theory]
    [InlineData("overwrite", ImportMode.Overwrite)]
    [InlineData("skip", ImportMode.Skip)]
    [InlineData("replace", ImportMode.Replace)]
    [InlineData("Replace", ImportMode.Replace)]
    public async Task Mode_IsParsedCaseInsensitively(string value, ImportMode expected)
    {
        cli.Service.Returns(TestData.Initialized());

        await cli.RunImportAsync("--mode", value, "--dry-run");

        Assert.Equal(expected, Assert.Single(cli.Service.AnalyzeRequests).Mode);
    }

    [Fact]
    public async Task Request_CarriesTheProjectFileContentSectionAndDefaultMode()
    {
        cli.Service.Returns(TestData.Initialized());

        await cli.RunImportAsync("--section", "App:Auth", "--dry-run");

        ImportRequest request = Assert.Single(cli.Service.AnalyzeRequests);
        Assert.Equal(TestData.Project.Path, request.ProjectPath);
        Assert.Equal(Path.GetFullPath(cli.JsonPath), request.Json.DisplayName);
        Assert.Equal("""{ "Api": { "Key": "x" } }""", request.Json.Content);
        Assert.Equal("App:Auth", request.Section);
        Assert.Equal(ImportMode.Overwrite, request.Mode);
    }

    [Fact]
    public async Task ShortAliases_AreAccepted()
    {
        cli.Service.Returns(TestData.Initialized(TestData.SomeChanges()));

        int exitCode = await cli.RunAsync("-p", TestData.Project.Path, "-f", cli.JsonPath, "-m", "skip", "-s", "Db", "-y");

        Assert.Equal(ExitCodes.Success, exitCode);
        ImportRequest request = Assert.Single(cli.Service.AnalyzeRequests);
        Assert.Equal(ImportMode.Skip, request.Mode);
        Assert.Equal("Db", request.Section);
        Assert.Single(cli.Service.ApplyCalls);
    }

    [Fact]
    public async Task FileDash_ReadsTheJsonFromStdin()
    {
        cli.Stdin = new StringReader("""{ "FromStdin": "1" }""");
        cli.Service.Returns(TestData.Initialized());

        int exitCode = await cli.RunAsync("--project", TestData.Project.Path, "--file", "-", "--dry-run");

        Assert.Equal(ExitCodes.Success, exitCode);
        ImportRequest request = Assert.Single(cli.Service.AnalyzeRequests);
        Assert.Equal(ImportFlow.StdinDisplayName, request.Json.DisplayName);
        Assert.Equal("""{ "FromStdin": "1" }""", request.Json.Content);
    }
}
