using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Microsoft.Extensions.DependencyInjection;

using SecretImport.Core;
using SecretImport.Desktop.Services;
using SecretImport.Desktop.Tests.Fakes;
using SecretImport.Desktop.ViewModels;
using SecretImport.Desktop.Views;

namespace SecretImport.Desktop.Tests;

public sealed class MainWindowHeadlessTests(HeadlessSession headless) : IDisposable
{
    private readonly FakeImportService _service = new();
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public Task MainWindow_FromDi_IsBoundToItsViewModel()
    {
        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            window.Show();

            MainWindowViewModel vm = Assert.IsType<MainWindowViewModel>(window.DataContext);
            Assert.Equal("Secret Import", window.Title);
            Assert.Equal("Secret Import", Find<TextBlock>(window, "HeaderText").Text);

            vm.Title = "Changed";

            Assert.Equal("Changed", window.Title);
            Assert.Equal("Changed", Find<TextBlock>(window, "HeaderText").Text);
            window.Close();
        });
    }

    [Fact]
    public Task Wizard_ShowsOneStepAtATime_AndMasksValuesInTheDiffGrid()
    {
        string json = _temp.File("in.json", """{ "Db": { "Password": "new-secret" } }""");
        _service.Discover = static _ => [TestData.Project("App"), TestData.Project("Lib", null)];

        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            MainWindowViewModel vm = (MainWindowViewModel)window.DataContext!;
            window.Show();

            // Project step: both projects listed with their status; picking one enables Next.
            Find<TextBox>(window, "ProjectPathBox").Text = "repo";
            Find<Button>(window, "FindProjectsButton").Command!.Execute(null);
            Pump(window);
            AssertOnlyVisibleStep(window, "ProjectStep");
            ListBox list = Find<ListBox>(window, "ProjectList");
            Assert.Equal(2, list.ItemCount);
            Assert.Contains(TextsIn(list), static t => t == "not initialized");
            Button next = Find<Button>(window, "ContinueToJsonButton");
            Assert.False(next.IsEffectivelyEnabled);
            list.SelectedIndex = 0;
            Pump(window);
            Assert.True(next.IsEffectivelyEnabled);
            next.Command!.Execute(null);
            Pump(window);

            // JSON step.
            AssertOnlyVisibleStep(window, "JsonStep");
            Assert.Equal("Project: App", Find<TextBlock>(window, "JsonProjectText").Text);
            Find<TextBox>(window, "JsonPathBox").Text = json;
            Find<Button>(window, "AnalyzeButton").Command!.Execute(null);
            Pump(window);

            // Confirm step: values masked, one row revealed on demand.
            AssertOnlyVisibleStep(window, "ConfirmStep");
            Assert.Equal("id-1", Find<SelectableTextBlock>(window, "SecretsIdText").Text);
            ItemsControl grid = Find<ItemsControl>(window, "DiffGrid");
            Assert.Equal(2, grid.ItemCount);
            Assert.DoesNotContain("new-secret", TextsIn(grid));
            Assert.Contains(DiffRowViewModel.Mask, TextsIn(grid));
            Button reveal = grid.GetLogicalDescendants().OfType<Button>().First(static b => b.Classes.Contains("reveal"));
            Assert.Equal("Show", reveal.Content);
            reveal.Command!.Execute(null);
            Pump(window);
            Assert.Contains("new-secret", TextsIn(grid));
            Assert.Equal("Hide", reveal.Content);

            // Back: the revealed values are gone from the window.
            vm.BackToJsonCommand.Execute(null);
            Pump(window);
            AssertOnlyVisibleStep(window, "JsonStep");
            Assert.Equal(0, grid.ItemCount);
            Assert.DoesNotContain("new-secret", TextsIn(window));
            window.Close();
        });
    }

    [Fact]
    public Task InitStep_WithoutSdk_DisablesInitAndShowsTheManualCommand()
    {
        string json = _temp.File("in.json", "{}");
        _service.Discover = static _ => [TestData.Project("App", null)];
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true, canInit: false);

        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            MainWindowViewModel vm = (MainWindowViewModel)window.DataContext!;
            window.Show();

            GoToAnalysis(vm, json);
            Pump(window);

            AssertOnlyVisibleStep(window, "InitStep");
            Assert.True(Find<StackPanel>(window, "ManualInitPanel").IsVisible);
            Assert.StartsWith("dotnet user-secrets init --project", Find<SelectableTextBlock>(window, "ManualInitCommand").Text);
            Assert.False(Find<Button>(window, "InitButton").IsEffectivelyEnabled);
            window.Close();
        });
    }

    [Fact]
    public Task ProjectStep_ShowsDiscoveryWarnings()
    {
        _service.Discover = static _ => [TestData.Project("App")];
        _service.DiscoverWarnings = [new ImportWarning(ImportWarningKind.MissingProjectFile, "Gone.csproj is listed in All.slnx but does not exist.")];

        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            MainWindowViewModel vm = (MainWindowViewModel)window.DataContext!;
            window.Show();
            Pump(window);
            Assert.False(Find<ItemsControl>(window, "DiscoveryWarningsList").IsVisible);

            vm.ProjectPath = "All.slnx";
            vm.DiscoverCommand.Execute(null);
            Pump(window);

            ItemsControl warnings = Find<ItemsControl>(window, "DiscoveryWarningsList");
            Assert.True(warnings.IsVisible);
            Assert.Contains("Gone.csproj is listed in All.slnx but does not exist.", TextsIn(warnings));
            window.Close();
        });
    }

    [Fact]
    public Task ConfirmAndResult_ShowWarningsCountsAndBackup()
    {
        string json = _temp.File("in.json", "{}");
        _service.Discover = static _ => [TestData.Project("App")];
        _service.Analyze = static r => TestData.Analysis(r, warnings: [new ImportWarning(ImportWarningKind.SharedSecretsId, "Shared with Lib.")]);
        _service.Apply = static _ => new ImportResult(3, 2, 1, "s/secrets.json", "s/secrets.json.bak");

        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            MainWindowViewModel vm = (MainWindowViewModel)window.DataContext!;
            window.Show();

            GoToAnalysis(vm, json);
            Pump(window);
            Assert.True(Find<ItemsControl>(window, "WarningsList").IsVisible);
            Assert.Contains("Shared with Lib.", TextsIn(Find<ItemsControl>(window, "WarningsList")));
            Assert.False(Find<TextBlock>(window, "NoChangesText").IsVisible);
            ComboBox mode = Find<ComboBox>(window, "ModeBox");
            Assert.Equal(ImportMode.Overwrite, mode.SelectedItem);

            mode.SelectedItem = ImportMode.Replace;
            Pump(window);
            Assert.Equal(ImportMode.Replace, _service.AnalyzeCalls[^1].Mode);

            Find<Button>(window, "ApplyButton").Command!.Execute(null);
            Pump(window);

            AssertOnlyVisibleStep(window, "ResultStep");
            Assert.Equal("+3 added   ~2 changed   -1 removed", Find<TextBlock>(window, "ResultCountsText").Inlines!.Text);
            Assert.Equal("s/secrets.json.bak", Find<SelectableTextBlock>(window, "BackupPathText").Text);
            window.Close();
        });
    }

    [Fact]
    public Task Errors_AreShownInline()
    {
        _service.Discover = static _ => throw new ImportException(ImportErrorKind.NoProjectFound, "No project here.");

        return headless.RunAsync(() =>
        {
            using ServiceProvider services = BuildServices();
            MainWindow window = services.GetRequiredService<MainWindow>();
            MainWindowViewModel vm = (MainWindowViewModel)window.DataContext!;
            window.Show();

            vm.ProjectPath = "nowhere";
            vm.DiscoverCommand.Execute(null);
            Pump(window);

            SelectableTextBlock error = Find<SelectableTextBlock>(window, "ErrorText");
            Assert.True(error.IsVisible);
            Assert.Equal("No project here.", error.Text);
            window.Close();
        });
    }

    [Fact]
    public Task DefaultFilePicker_UsesTheMainWindowStorageProvider()
    {
        return headless.RunAsync(() =>
        {
            using ServiceProvider services = DesktopServices.Build(s => s.AddSingleton<IImportService>(_service));
            MainWindow window = services.GetRequiredService<MainWindow>();
            window.Show();

            StorageProviderFilePicker picker = Assert.IsType<StorageProviderFilePicker>(services.GetRequiredService<IFilePickerService>());
            Task<string?> pick = picker.PickFolderAsync();

            // The headless platform has no real folder picker, so nothing is picked.
            Assert.True(pick.IsCompleted);
            Assert.Null(pick.Result);
            window.Close();
        });
    }

    private ServiceProvider BuildServices()
    {
        return DesktopServices.Build(s =>
        {
            s.AddSingleton<IImportService>(_service);
            s.AddSingleton<IFilePickerService>(new FakeFilePicker());
            s.AddSingleton<IRecentProjectsStore>(new FakeRecentProjectsStore());
        });
    }

    private static void GoToAnalysis(MainWindowViewModel vm, string json)
    {
        vm.ProjectPath = "repo";
        vm.DiscoverCommand.Execute(null);
        vm.ContinueToJsonCommand.Execute(null);
        vm.JsonPath = json;
        vm.AnalyzeCommand.Execute(null);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name)
        where T : Control
    {
        return window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' not found.");
    }

    private static void AssertOnlyVisibleStep(Window window, string visible)
    {
        foreach (string step in (string[])["ProjectStep", "JsonStep", "InitStep", "ConfirmStep", "ResultStep"])
        {
            Assert.Equal(step == visible, Find<Control>(window, step).IsVisible);
        }
    }

    private static List<string?> TextsIn(ILogical root)
    {
        return root.GetLogicalDescendants().OfType<TextBlock>().Select(static t => t.Text).ToList();
    }
}
