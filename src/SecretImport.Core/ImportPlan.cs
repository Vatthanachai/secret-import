namespace SecretImport.Core;

public sealed record SecretChange(string Key, SecretChangeKind Kind, string? OldValue, string? NewValue);

public sealed record ImportPlan(IReadOnlyList<SecretChange> Changes, IReadOnlyDictionary<string, string> Result)
{
    /// <summary>True when at least one change is <see cref="SecretChangeKind.Added"/>, <see cref="SecretChangeKind.Changed"/> or <see cref="SecretChangeKind.Removed"/>.</summary>
    public bool HasChanges => Changes.Any(static c => c.Kind is SecretChangeKind.Added or SecretChangeKind.Changed or SecretChangeKind.Removed);
}
