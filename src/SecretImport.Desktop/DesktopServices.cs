using Avalonia.Platform.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SecretImport.Core;

using SecretImport.Desktop.Services;
using SecretImport.Desktop.ViewModels;
using SecretImport.Desktop.Views;

namespace SecretImport.Desktop;

/// <summary>DI composition root of the desktop app. Kept out of <c>App.axaml.cs</c> so it is unit-tested.</summary>
public static class DesktopServices
{
    /// <summary>
    /// Builds the container. <paramref name="configure"/> runs after the desktop registrations, so it can add the Core
    /// services (<c>services.AddSecretImportCore()</c>) and replace any desktop service (tests swap in fakes this way).
    /// </summary>
    public static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddSecretImportDesktop();
        configure?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Desktop services only. <see cref="IImportService"/> must be registered separately.</summary>
    public static IServiceCollection AddSecretImportDesktop(this IServiceCollection services)
    {
        services.AddSingleton<IRecentProjectsStore>(static _ => new JsonRecentProjectsStore(JsonRecentProjectsStore.DefaultFilePath));
        services.AddSingleton<IFilePickerService>(static sp =>
            new StorageProviderFilePicker(() => MainWindowStorageProvider(sp)));
        // Core's rule for the shared-id scope (ADR-0013 Amendment 1); TryAdd so AddSecretImportCore and tests share one instance.
        services.TryAddSingleton(static _ => new SolutionScopeResolver());
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton(static sp => new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });
        return services;
    }

    private static IStorageProvider MainWindowStorageProvider(IServiceProvider services)
    {
        return services.GetRequiredService<MainWindow>().StorageProvider;
    }
}
