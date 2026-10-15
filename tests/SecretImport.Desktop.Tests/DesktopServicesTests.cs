using Microsoft.Extensions.DependencyInjection;

using SecretImport.Core;
using SecretImport.Desktop.Services;
using SecretImport.Desktop.Tests.Fakes;
using SecretImport.Desktop.ViewModels;

namespace SecretImport.Desktop.Tests;

public class DesktopServicesTests
{
    [Fact]
    public void Build_RegistersTheDesktopServices()
    {
        using ServiceProvider services = DesktopServices.Build();

        JsonRecentProjectsStore store = Assert.IsType<JsonRecentProjectsStore>(services.GetRequiredService<IRecentProjectsStore>());
        Assert.Equal(JsonRecentProjectsStore.DefaultFilePath, store.FilePath);
        Assert.IsType<StorageProviderFilePicker>(services.GetRequiredService<IFilePickerService>());
        Assert.Same(services.GetRequiredService<IFilePickerService>(), services.GetRequiredService<IFilePickerService>());
        Assert.Same(services.GetRequiredService<SolutionScopeResolver>(), services.GetRequiredService<SolutionScopeResolver>());
    }

    [Fact]
    public void Build_WithoutCore_CannotCreateTheViewModel()
    {
        using ServiceProvider services = DesktopServices.Build();

        Assert.Null(services.GetService<IImportService>());
        Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<MainWindowViewModel>());
    }

    [Fact]
    public void Build_WithCore_ResolvesTheViewModel()
    {
        using ServiceProvider services = DesktopServices.Build(static s =>
        {
            s.AddSecretImportCore();
            s.AddSingleton<IRecentProjectsStore>(new FakeRecentProjectsStore());
        });

        Assert.NotNull(services.GetRequiredService<IImportService>());
        Assert.NotNull(services.GetRequiredService<SolutionScopeResolver>());
        Assert.NotNull(services.GetRequiredService<MainWindowViewModel>());
    }

    [Fact]
    public void Configure_AddsAndReplacesServices()
    {
        FakeImportService service = new();
        FakeRecentProjectsStore recent = new("a.csproj");

        SolutionScopeResolver resolver = new(static _ => []);

        using ServiceProvider services = DesktopServices.Build(s =>
        {
            s.AddSingleton<IImportService>(service);
            s.AddSingleton<IRecentProjectsStore>(recent);
            s.AddSingleton(resolver);
        });

        Assert.Same(service, services.GetRequiredService<IImportService>());
        Assert.Same(resolver, services.GetRequiredService<SolutionScopeResolver>());
        Assert.Same(recent, services.GetRequiredService<IRecentProjectsStore>());
        MainWindowViewModel vm = services.GetRequiredService<MainWindowViewModel>();
        Assert.Equal(["a.csproj"], vm.RecentProjects);
        Assert.NotSame(vm, services.GetRequiredService<MainWindowViewModel>());
    }
}
