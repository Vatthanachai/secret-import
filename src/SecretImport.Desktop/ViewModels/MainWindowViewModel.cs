using CommunityToolkit.Mvvm.ComponentModel;

namespace SecretImport.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "Secret Import";
}
