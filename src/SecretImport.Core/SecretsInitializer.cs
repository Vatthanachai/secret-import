namespace SecretImport.Core;

/// <summary>Initializes user secrets through the official tool, so this tool never edits project files itself (ADR-0002, ADR-0005).</summary>
internal sealed class SecretsInitializer(IDotnetCli dotnet)
{
    /// <exception cref="ImportException">
    /// <see cref="ImportErrorKind.SdkUnavailable"/> when the SDK is missing (the message names the command to run by hand);
    /// <see cref="ImportErrorKind.IoFailure"/> when the command fails.
    /// </exception>
    public async Task InitAsync(ProjectRef project, CancellationToken ct = default)
    {
        if (!await dotnet.IsSdkAvailableAsync(ct).ConfigureAwait(false))
        {
            throw new ImportException(
                ImportErrorKind.SdkUnavailable,
                $"The .NET SDK is required to initialize user secrets. Install it, or run this yourself: dotnet user-secrets init --project \"{project.Path}\"");
        }

        DotnetResult result = await dotnet.RunAsync(
            ["user-secrets", "init", "--project", project.Path],
            Path.GetDirectoryName(project.Path),
            ct).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            string output = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new ImportException(ImportErrorKind.IoFailure, $"'dotnet user-secrets init' failed for {project.Path} (exit code {result.ExitCode}): {output.Trim()}");
        }
    }
}