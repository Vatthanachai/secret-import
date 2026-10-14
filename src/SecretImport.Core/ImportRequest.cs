namespace SecretImport.Core;

public sealed record ImportRequest(string ProjectPath, JsonInput Json, ImportMode Mode, string? Section);
