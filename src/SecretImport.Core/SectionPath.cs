namespace SecretImport.Core;

/// <summary>The <c>--section</c> rules shared by import and export (ADR-0004): a case-insensitive <c>A:B</c> path; keys keep the prefix.</summary>
internal static class SectionPath
{
    /// <summary>Trims whitespace and surrounding colons; null when nothing is left (no filtering).</summary>
    public static string? Normalize(string? section)
    {
        string? trimmed = section?.Trim().Trim(':').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>True when <paramref name="key"/> is the section itself or lies below it.</summary>
    public static bool Contains(string section, string key) =>
        key.Equals(section, StringComparison.OrdinalIgnoreCase)
        || (key.Length > section.Length
            && key.StartsWith(section, StringComparison.OrdinalIgnoreCase)
            && key[section.Length] == ':');

    /// <summary>The values inside <paramref name="section"/> (already normalized), in their original order.</summary>
    /// <exception cref="ImportException"><see cref="ImportErrorKind.InvalidInput"/> when the section has no values.</exception>
    public static Dictionary<string, string> Filter(IEnumerable<KeyValuePair<string, string>> values, string section, string sourceName)
    {
        Dictionary<string, string> filtered = values.Where(p => Contains(section, p.Key))
            .ToDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (filtered.Count == 0)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"Section '{section}' was not found in {sourceName} (or it has no values).");
        }

        return filtered;
    }
}
