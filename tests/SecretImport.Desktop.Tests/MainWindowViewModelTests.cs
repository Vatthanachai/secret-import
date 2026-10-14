using System.ComponentModel;

using SecretImport.Desktop.ViewModels;

namespace SecretImport.Desktop.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void Title_DefaultsToAppName()
    {
        Assert.Equal("Secret Import", new MainWindowViewModel().Title);
    }

    [Fact]
    public void SettingTitle_RaisesPropertyChanged()
    {
        MainWindowViewModel vm = new();
        List<string?> raised = [];
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Title = "Other";

        Assert.Equal("Other", vm.Title);
        Assert.Equal([nameof(MainWindowViewModel.Title)], raised);
    }
}
