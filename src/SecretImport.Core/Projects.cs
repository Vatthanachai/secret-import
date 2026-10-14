namespace SecretImport.Core;

public sealed record ProjectRef(string Path, string Name);

public sealed record ProjectInfo(ProjectRef Project, string? UserSecretsId, SecretsIdSource Source, IReadOnlyList<ProjectRef> SharedWith);
