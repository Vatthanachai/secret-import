using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SecretImport.Core;

internal sealed record DotnetResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Every <c>dotnet</c> process call goes through this seam so tests can fake it (ADR-0002).</summary>
internal interface IDotnetCli
{
    /// <summary>True when <c>dotnet --list-sdks</c> runs and reports at least one SDK.</summary>
    Task<bool> IsSdkAvailableAsync(CancellationToken ct = default);

    /// <exception cref="ImportException"><see cref="ImportErrorKind.SdkUnavailable"/> when <c>dotnet</c> cannot be started or times out.</exception>
    Task<DotnetResult> RunAsync(IReadOnlyList<string> arguments, string? workingDirectory = null, CancellationToken ct = default);
}

/// <summary>Runs the real <c>dotnet</c> host with a timeout.</summary>
internal sealed class DotnetCli : IDotnetCli
{
    private bool? _sdkAvailable;

    public DotnetCli()
        : this("dotnet", TimeSpan.FromMinutes(2))
    {
    }

    public DotnetCli(string executable, TimeSpan timeout)
    {
        Executable = executable;
        Timeout = timeout;
    }

    public string Executable { get; }

    public TimeSpan Timeout { get; }

    public async Task<bool> IsSdkAvailableAsync(CancellationToken ct = default)
    {
        if (_sdkAvailable is bool known)
        {
            return known;
        }

        try
        {
            DotnetResult result = await RunAsync(["--list-sdks"], null, ct).ConfigureAwait(false);
            _sdkAvailable = result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.SdkUnavailable)
        {
            _sdkAvailable = false;
        }

        return _sdkAvailable.Value;
    }

    public async Task<DotnetResult> RunAsync(IReadOnlyList<string> arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using Process process = new() { StartInfo = BuildStartInfo(arguments, workingDirectory) };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new ImportException(ImportErrorKind.SdkUnavailable, $"Cannot run '{Executable}'. Install the .NET SDK and make sure it is on PATH.", innerException: ex);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            ct.ThrowIfCancellationRequested();
            throw new ImportException(ImportErrorKind.SdkUnavailable, $"'{Executable} {arguments.FirstOrDefault()}' timed out after {Timeout.TotalSeconds:0.###} s.");
        }

        return new DotnetResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    internal ProcessStartInfo BuildStartInfo(IReadOnlyList<string> arguments, string? workingDirectory)
    {
        ProcessStartInfo info = new(Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // When this runs under another dotnet/MSBuild process (e.g. `dotnet test`), inherited MSBuild variables
        // point the child at the parent's SDK and break evaluation.
        foreach (string key in info.Environment.Keys.Where(static k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(key);
        }

        info.Environment["DOTNET_NOLOGO"] = "1";
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return info;
    }
}