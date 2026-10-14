namespace SecretImport.Core;

/// <param name="ScopePath">
/// Optional folder or <c>.sln</c>/<c>.slnx</c> whose projects are compared with the target to find a shared UserSecretsId (ADR-0013).
/// Null skips the shared-id check.
/// </param>
public sealed record ImportRequest(string ProjectPath, JsonInput Json, ImportMode Mode, string? Section, string? ScopePath = null);
