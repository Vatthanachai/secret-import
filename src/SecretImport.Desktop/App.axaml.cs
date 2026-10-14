using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using SecretImport.Desktop.Views;

namespace SecretImport.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // IImportService comes from Core: once ImportService exists this becomes
            // DesktopServices.Build(static s => s.AddSecretImportCore()).
            ServiceProvider services = DesktopServices.Build();
            desktop.MainWindow = services.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
