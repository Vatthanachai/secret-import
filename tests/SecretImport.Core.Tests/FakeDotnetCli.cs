namespace SecretImport.Core.Tests;

/// <summary>Scriptable <see cref="IDotnetCli"/>; records every call.</summary>
internal sealed class FakeDotnetCli : IDotnetCli
{
    public bool SdkAvailable { get; set; } = true;

    /// <summary>Answers a call; may throw. Defaults to exit code 0 with no output.</summary>
    public Func<IReadOnlyList<string>, DotnetResult> Handler { get; set; } = static _ => new DotnetResult(0, string.Empty, string.Empty);

    public List<(IReadOnlyList<string> Arguments, string? WorkingDirectory)> Calls { get; } = [];

    public Task<bool> IsSdkAvailableAsync(CancellationToken ct = default) => Task.FromResult(SdkAvailable);

    public Task<DotnetResult> RunAsync(IReadOnlyList<string> arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        Calls.Add((arguments, workingDirectory));
        return Task.FromResult(Handler(arguments));
    }

    public static FakeDotnetCli Unavailable() => new()
    {
        SdkAvailable = false,
        Handler = static _ => throw new ImportException(ImportErrorKind.SdkUnavailable, "no sdk"),
    };
}