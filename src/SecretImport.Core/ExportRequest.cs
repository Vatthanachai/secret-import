namespace SecretImport.Core;

/// <summary>What to export (ADR-0015).</summary>
/// <param name="ProjectPath">A project file or a folder/solution that contains exactly one project (same rules as import, ADR-0006).</param>
/// <param name="Section">Optional configuration path (e.g. <c>ConnectionStrings</c>); keys keep the section prefix.</param>
/// <param name="Flat">True writes the keys as stored (<c>A:B</c>); false (default) nests them into objects and arrays.</param>
public sealed record ExportRequest(string ProjectPath, string? Section = null, bool Flat = false);

/// <param name="SecretsPath">The <c>secrets.json</c> that was read (it may not exist).</param>
/// <param name="KeyCount">The number of flat keys exported.</param>
/// <param name="Json">The exported document: indented, UTF-8 friendly, ending with a newline. Contains secret values.</param>
/// <param name="Warnings">Warnings found while resolving the project and reading the store, e.g. <see cref="ImportWarningKind.InsecureFilePermissions"/>.</param>
public sealed record ExportResult(ProjectInfo Project, string SecretsPath, int KeyCount, string Json, IReadOnlyList<ImportWarning> Warnings);
