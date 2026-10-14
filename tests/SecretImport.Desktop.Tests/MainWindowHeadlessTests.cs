using Avalonia.Controls;

using Microsoft.Extensions.DependencyInjection;

using SecretImport.Desktop.ViewModels;
using SecretImport.Desktop.Views;

namespace SecretImport.Desktop.Tests;

public class MainWindowHeadlessTests(HeadlessSession headless)
{
    [Fact]
    public Task MainWindow_FromDi_IsBoundToItsViewModel()
    {
        return headless.RunAsync(static () =>
        {
            using ServiceProvider services = DesktopServices.Build();

            MainWindow window = services.GetRequiredService<MainWindow>();
            window.Show();

            MainWindowViewModel vm = Assert.IsType<MainWindowViewModel>(window.DataContext);
            Assert.Equal("Secret Import", window.Title);
            Assert.Equal("Secret Import", window.FindControl<TextBlock>("HeaderText")?.Text);

            vm.Title = "Changed";

            Assert.Equal("Changed", window.Title);
            Assert.Equal("Changed", window.FindControl<TextBlock>("HeaderText")?.Text);
            window.Close();
        });
    }

    [Fact]
    public void Services_ResolveANewViewModelEachTime()
    {
        using ServiceProvider services = DesktopServices.Build();

        Assert.NotSame(services.GetRequiredService<MainWindowViewModel>(), services.GetRequiredService<MainWindowViewModel>());
    }
}
