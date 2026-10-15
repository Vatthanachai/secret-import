using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SecretImport.Core;

namespace SecretImport.Desktop.ViewModels;

/// <summary>One row of the diff grid. Values are masked until the user reveals that row.</summary>
public sealed partial class DiffRowViewModel(SecretChange change) : ViewModelBase
{
    public const string Mask = "••••";

    private readonly SecretChange _change = change;

    public string Key => _change.Key;

    public SecretChangeKind Kind => _change.Kind;

    public string KindLabel => Kind switch
    {
        SecretChangeKind.Added => "+ added",
        SecretChangeKind.Changed => "~ changed",
        SecretChangeKind.Unchanged => "= unchanged",
        SecretChangeKind.Removed => "- removed",
        _ => "skipped",
    };

    public bool HasValues => _change.OldValue is not null || _change.NewValue is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OldValueDisplay), nameof(NewValueDisplay), nameof(RevealLabel))]
    public partial bool IsRevealed { get; set; }

    public string OldValueDisplay => Display(_change.OldValue);

    public string NewValueDisplay => Display(_change.NewValue);

    public string RevealLabel => IsRevealed ? "Hide" : "Show";

    [RelayCommand]
    private void ToggleReveal()
    {
        IsRevealed = !IsRevealed;
    }

    private string Display(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return IsRevealed ? value : Mask;
    }
}
