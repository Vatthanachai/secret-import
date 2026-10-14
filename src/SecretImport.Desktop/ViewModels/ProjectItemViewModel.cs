using SecretImport.Core;

namespace SecretImport.Desktop.ViewModels;

public enum ProjectStatus { Initialized, NotInitialized, SharedId }

/// <summary>One discovered project in the project list, with its user-secrets status.</summary>
public sealed class ProjectItemViewModel(ProjectInfo info)
{
    public ProjectInfo Info { get; } = info;

    public string Name => Info.Project.Name;

    public string Path => Info.Project.Path;

    public string? UserSecretsId => Info.UserSecretsId;

    public ProjectStatus Status => Info.UserSecretsId is null
        ? ProjectStatus.NotInitialized
        : Info.SharedWith.Count > 0 ? ProjectStatus.SharedId : ProjectStatus.Initialized;

    public string StatusGlyph => Status switch
    {
        ProjectStatus.Initialized => "✅",
        ProjectStatus.NotInitialized => "⚠️",
        _ => "\U0001F517",
    };

    public string StatusText => Status switch
    {
        ProjectStatus.Initialized => "initialized",
        ProjectStatus.NotInitialized => "not initialized",
        _ => "shares its UserSecretsId with " + string.Join(", ", Info.SharedWith.Select(static p => p.Name)),
    };
}
