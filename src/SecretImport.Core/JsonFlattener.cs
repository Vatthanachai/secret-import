using System.Text.Json;

namespace SecretImport.Core;

/// <summary>The flat, case-insensitive key/value view of a JSON document, in document order.</summary>
internal sealed record FlattenResult(IReadOnlyDictionary<string, string> Values, IReadOnlyList<ImportWarning> Warnings);

/// <summary>
/// Flattens a JSON(C) document into .NET configuration keys (ADR-0004): nested objects become <c>Parent:Child</c>,
/// arrays become <c>Items:0</c>, numbers and booleans keep their JSON text, <c>null</c> is skipped with a warning.
/// Error messages name keys and positions but never values.
/// </summary>
internal sealed class JsonFlattener
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <param name="json">The document text.</param>
    /// <param name="section">Optional configuration path (e.g. <c>ConnectionStrings</c>); keys keep the section prefix.</param>
    /// <param name="sourceName">Where the JSON came from, used in error messages.</param>
    /// <exception cref="ImportException"><see cref="ImportErrorKind.InvalidInput"/> for malformed JSON, a non-object root, duplicate keys or a missing section.</exception>
    public FlattenResult Flatten(string json, string? section, string sourceName)
    {
        using JsonDocument document = Parse(json, sourceName);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"The root of {sourceName} must be a JSON object, but it is {Describe(document.RootElement.ValueKind)}.");
        }

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        List<string> nullKeys = [];
        Visit(document.RootElement, null, values, nullKeys, sourceName);

        string? prefix = NormalizeSection(section);
        if (prefix is not null)
        {
            values = values.Where(p => IsInSection(p.Key, prefix))
                .ToDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);
            nullKeys.RemoveAll(k => !IsInSection(k, prefix));
            if (values.Count == 0)
            {
                throw new ImportException(ImportErrorKind.InvalidInput, $"Section '{prefix}' was not found in {sourceName} (or it has no values).");
            }
        }

        ImportWarning[] warnings = [.. nullKeys.Select(static k => new ImportWarning(ImportWarningKind.NullValueSkipped, $"Key '{k}' is null and was skipped."))];
        return new FlattenResult(values, warnings);
    }

    private static JsonDocument Parse(string json, string sourceName)
    {
        try
        {
            return JsonDocument.Parse(json, Options);
        }
        catch (JsonException ex)
        {
            // Deliberately no inner exception: JsonException messages can quote the offending input.
            long line = (ex.LineNumber ?? 0) + 1;
            long position = (ex.BytePositionInLine ?? 0) + 1;
            throw new ImportException(ImportErrorKind.InvalidInput, $"{sourceName} is not valid JSON (line {line}, byte position {position}).");
        }
    }

    private static void Visit(JsonElement element, string? path, Dictionary<string, string> values, List<string> nullKeys, string sourceName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    Visit(property.Value, Combine(path, property.Name), values, nullKeys, sourceName);
                }

                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Visit(item, Combine(path, index.ToString(System.Globalization.CultureInfo.InvariantCulture)), values, nullKeys, sourceName);
                    index++;
                }

                break;
            case JsonValueKind.Null:
                AddKey(path!, null, values, nullKeys, sourceName);
                break;
            case JsonValueKind.String:
                AddKey(path!, element.GetString()!, values, nullKeys, sourceName);
                break;
            default:
                // Number, True, False: the raw JSON text is culture-invariant ("5432", "1.5", "true").
                AddKey(path!, element.GetRawText(), values, nullKeys, sourceName);
                break;
        }
    }

    private static void AddKey(string key, string? value, Dictionary<string, string> values, List<string> nullKeys, string sourceName)
    {
        string? existing = values.ContainsKey(key)
            ? values.Keys.First(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            : nullKeys.Find(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            string names = string.Equals(existing, key, StringComparison.Ordinal) ? $"'{key}'" : $"'{existing}' and '{key}'";
            throw new ImportException(ImportErrorKind.InvalidInput, $"Duplicate key {names} in {sourceName} (keys are case-insensitive after flattening). Nothing was imported.");
        }

        if (value is null)
        {
            nullKeys.Add(key);
        }
        else
        {
            values.Add(key, value);
        }
    }

    private static string Combine(string? path, string name) => path is null ? name : $"{path}:{name}";

    private static string? NormalizeSection(string? section)
    {
        string? trimmed = section?.Trim().Trim(':').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsInSection(string key, string section) =>
        key.Equals(section, StringComparison.OrdinalIgnoreCase)
        || (key.Length > section.Length
            && key.StartsWith(section, StringComparison.OrdinalIgnoreCase)
            && key[section.Length] == ':');

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.Null => "null",
        _ => "a boolean",
    };
}