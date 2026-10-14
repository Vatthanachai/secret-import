using Avalonia;
using Avalonia.Headless;

namespace SecretImport.Desktop.Tests;

/// <summary>Entry point that <see cref="HeadlessUnitTestSession"/> uses to build the app on the headless platform.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
