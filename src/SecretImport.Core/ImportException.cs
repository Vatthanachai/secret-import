namespace SecretImport.Core;

public sealed class ImportException : Exception
{
    public ImportException(ImportErrorKind kind, string message, IReadOnlyList<ProjectRef>? candidates = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        Candidates = candidates ?? [];
    }

    public ImportErrorKind Kind { get; }

    /// <summary>The candidate projects, used with <see cref="ImportErrorKind.MultipleProjects"/>. Empty otherwise.</summary>
    public IReadOnlyList<ProjectRef> Candidates { get; }
}
