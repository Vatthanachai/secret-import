using Microsoft.Extensions.DependencyInjection;

using SecretImport.Desktop.ViewModels;
using SecretImport.Desktop.Views;

namespace SecretImport.Desktop;

/// <summary>DI composition root of the desktop app. Kept out of <c>App.axaml.cs</c> so it is unit-tested.</summary>
public static class DesktopServices
{
    public static ServiceProvider Build()
    {
        return new ServiceCollection().AddSecretImportDesktop().BuildServiceProvider(validateScopes: true);
    }

    public static IServiceCollection AddSecretImportDesktop(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient(static sp => new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });
        return services;
    }
}
