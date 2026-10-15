namespace SecretImport.Core;

public sealed record ProjectRef(string Path, string Name);

public sealed record ProjectInfo(ProjectRef Project, string? UserSecretsId, SecretsIdSource Source, IReadOnlyList<ProjectRef> SharedWith);

/// <param name="Warnings">Problems found while reading the folder or solution, such as a listed project file that does not exist.</param>
public sealed record DiscoveryResult(IReadOnlyList<ProjectInfo> Projects, IReadOnlyList<ImportWarning> Warnings);
