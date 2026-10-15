namespace SecretImport.Core;

/// <summary>Builds the per-key diff and the resulting secrets for a merge mode (ADR-0003).</summary>
internal sealed class ImportPlanner
{
    /// <param name="existing">The secrets currently stored (may be empty).</param>
    /// <param name="incoming">The flattened import.</param>
    /// <param name="mode">How to merge.</param>
    /// <returns>
    /// Changes sorted by key; existing-only keys are listed only when removed (<see cref="ImportMode.Replace"/>).
    /// The result keeps existing keys in their order and spelling, followed by added keys in import order
    /// (for <see cref="ImportMode.Replace"/>: the import order).
    /// </returns>
    public ImportPlan Plan(IReadOnlyDictionary<string, string> existing, IReadOnlyDictionary<string, string> incoming, ImportMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown import mode.");
        }

        // Configuration keys are case-insensitive, whatever comparer the caller's dictionary uses.
        Dictionary<string, (string Key, string Value)> current = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in existing)
        {
            current[key] = (key, value);
        }

        List<SecretChange> changes = [];
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (mode != ImportMode.Replace)
        {
            foreach ((string key, string value) in current.Values)
            {
                result[key] = value;
            }
        }

        foreach ((string incomingKey, string newValue) in incoming)
        {
            if (!current.TryGetValue(incomingKey, out (string Key, string Value) old))
            {
                changes.Add(new SecretChange(incomingKey, SecretChangeKind.Added, null, newValue));
                result[incomingKey] = newValue;
                continue;
            }

            SecretChangeKind kind = string.Equals(old.Value, newValue, StringComparison.Ordinal) ? SecretChangeKind.Unchanged
                : mode == ImportMode.Skip ? SecretChangeKind.Skipped
                : SecretChangeKind.Changed;
            changes.Add(new SecretChange(old.Key, kind, old.Value, newValue));
            result[old.Key] = kind == SecretChangeKind.Skipped ? old.Value : newValue;
        }

        if (mode == ImportMode.Replace)
        {
            changes.AddRange(current.Values
                .Where(p => !result.ContainsKey(p.Key))
                .Select(static p => new SecretChange(p.Key, SecretChangeKind.Removed, p.Value, null)));
        }

        changes.Sort(static (a, b) =>
        {
            int byKey = StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
            return byKey != 0 ? byKey : StringComparer.Ordinal.Compare(a.Key, b.Key);
        });
        return new ImportPlan(changes, result);
    }
}