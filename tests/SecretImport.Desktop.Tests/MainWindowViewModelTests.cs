using SecretImport.Core;
using SecretImport.Desktop.Tests.Fakes;
using SecretImport.Desktop.ViewModels;

namespace SecretImport.Desktop.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly FakeImportService _service = new();
    private readonly FakeFilePicker _picker = new();
    private readonly FakeRecentProjectsStore _recent = new("recent-a.csproj", "recent-b.csproj");
    private readonly TempDir _temp = new();
    private readonly string _jsonPath;
    private readonly MainWindowViewModel _vm;

    public MainWindowViewModelTests()
    {
        _jsonPath = _temp.File("secrets.json", """{ "Db": { "Password": "new-secret" } }""");
        _vm = new MainWindowViewModel(_service, _picker, _recent);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    // ---- Initial state ----

    [Fact]
    public void StartsOnTheProjectStepWithDefaults()
    {
        Assert.Equal("Secret Import", _vm.Title);
        Assert.Equal(WizardStep.Project, _vm.Step);
        Assert.True(_vm.IsProjectStep);
        Assert.False(_vm.IsJsonStep || _vm.IsInitStep || _vm.IsConfirmStep || _vm.IsResultStep);
        Assert.Equal(["recent-a.csproj", "recent-b.csproj"], _vm.RecentProjects);
        Assert.Equal([ImportMode.Overwrite, ImportMode.Skip, ImportMode.Replace], _vm.Modes);
        Assert.Equal(ImportMode.Overwrite, _vm.Mode);
        Assert.False(_vm.HasError);
        Assert.False(_vm.IsBusy);
        Assert.True(_vm.IsIdle);
        Assert.Null(_vm.Analysis);
        Assert.False(_vm.HasWarnings);
        Assert.False(_vm.HasNoChanges);
        Assert.False(_vm.CanInit);
        Assert.False(_vm.DiscoverCommand.CanExecute(null));
        Assert.False(_vm.ContinueToJsonCommand.CanExecute(null));
        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
        Assert.False(_vm.ReanalyzeCommand.CanExecute(null));
        Assert.False(_vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void SettingTitle_RaisesPropertyChanged()
    {
        List<string?> raised = [];
        _vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _vm.Title = "Other";

        Assert.Equal("Other", _vm.Title);
        Assert.Equal([nameof(MainWindowViewModel.Title)], raised);
    }

    // ---- Project step ----

    [Fact]
    public async Task Discover_WithOneProject_SelectsIt()
    {
        _service.Discover = static _ => [TestData.Project("App")];
        _vm.ProjectPath = "  some/folder  ";

        await _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.Equal(["some/folder"], _service.DiscoverCalls);
        ProjectItemViewModel item = Assert.Single(_vm.Projects);
        Assert.Same(item, _vm.SelectedProject);
        Assert.True(_vm.ContinueToJsonCommand.CanExecute(null));
        Assert.False(_vm.HasError);
    }

    [Fact]
    public async Task Discover_WithSeveralProjects_LetsTheUserPick()
    {
        _service.Discover = static _ => [TestData.Project("A"), TestData.Project("B", null)];
        _vm.ProjectPath = "repo.slnx";

        await _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.Equal(2, _vm.Projects.Count);
        Assert.Null(_vm.SelectedProject);
        Assert.False(_vm.ContinueToJsonCommand.CanExecute(null));

        _vm.SelectedProject = _vm.Projects[1];

        Assert.True(_vm.ContinueToJsonCommand.CanExecute(null));
    }

    [Fact]
    public async Task Discover_WithNoProjects_ShowsAnError()
    {
        _vm.ProjectPath = "empty";

        await _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.Empty(_vm.Projects);
        Assert.True(_vm.HasError);
        Assert.Contains("No .csproj", _vm.ErrorMessage);
    }

    [Fact]
    public async Task Discover_Failure_ShowsTheMessageAndClearsThePreviousList()
    {
        _service.Discover = static _ => [TestData.Project("A")];
        _vm.ProjectPath = "first";
        await _vm.DiscoverCommand.ExecuteAsync(null);

        _service.Discover = static _ => throw new ImportException(ImportErrorKind.NoProjectFound, "Nothing at 'missing'.");
        _vm.ProjectPath = "missing";
        await _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.Empty(_vm.Projects);
        Assert.Null(_vm.SelectedProject);
        Assert.Equal("Nothing at 'missing'.", _vm.ErrorMessage);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task UnexpectedExceptions_AreShownInline()
    {
        _service.Discover = static _ => throw new InvalidOperationException("boom");
        _vm.ProjectPath = "x";

        await _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.Equal("boom", _vm.ErrorMessage);
    }

    [Fact]
    public async Task BrowseProjectFile_DiscoversThePickedFile()
    {
        _picker.ProjectFile = "picked.csproj";
        _service.Discover = static _ => [TestData.Project("Picked")];

        await _vm.BrowseProjectFileCommand.ExecuteAsync(null);

        Assert.Equal("picked.csproj", _vm.ProjectPath);
        Assert.Equal(["picked.csproj"], _service.DiscoverCalls);
        Assert.NotNull(_vm.SelectedProject);
    }

    [Fact]
    public async Task BrowseProjectFolder_DiscoversThePickedFolder()
    {
        _picker.Folder = "picked-folder";

        await _vm.BrowseProjectFolderCommand.ExecuteAsync(null);

        Assert.Equal("picked-folder", _vm.ProjectPath);
        Assert.Equal(["picked-folder"], _service.DiscoverCalls);
    }

    [Fact]
    public async Task CancelledPickers_ChangeNothing()
    {
        _vm.ProjectPath = "unchanged";

        await _vm.BrowseProjectFileCommand.ExecuteAsync(null);
        await _vm.BrowseProjectFolderCommand.ExecuteAsync(null);
        await _vm.BrowseJsonCommand.ExecuteAsync(null);

        Assert.Equal("unchanged", _vm.ProjectPath);
        Assert.Equal(string.Empty, _vm.JsonPath);
        Assert.Empty(_service.DiscoverCalls);
    }

    [Fact]
    public async Task OpenRecent_DiscoversThatPath()
    {
        _service.Discover = static _ => [TestData.Project("Recent")];

        await _vm.OpenRecentCommand.ExecuteAsync("recent-b.csproj");

        Assert.Equal("recent-b.csproj", _vm.ProjectPath);
        Assert.Equal(["recent-b.csproj"], _service.DiscoverCalls);
        Assert.NotNull(_vm.SelectedProject);
    }

    [Fact]
    public async Task OpenRecent_WithNothingSelected_DoesNothing()
    {
        await _vm.OpenRecentCommand.ExecuteAsync(null);

        Assert.Empty(_service.DiscoverCalls);
    }

    [Fact]
    public async Task ContinueToJson_RemembersTheProjectAndMovesOn()
    {
        await GoToJsonStepAsync();

        Assert.Equal(WizardStep.Json, _vm.Step);
        Assert.True(_vm.IsJsonStep);
        string path = _vm.SelectedProject!.Path;
        Assert.Equal([path, "recent-a.csproj", "recent-b.csproj"], _recent.Paths);
        Assert.Equal([path, "recent-a.csproj", "recent-b.csproj"], _vm.RecentProjects);
    }

    [Fact]
    public async Task BackToProject_ReturnsToTheProjectStep()
    {
        await GoToJsonStepAsync();

        _vm.BackToProjectCommand.Execute(null);

        Assert.Equal(WizardStep.Project, _vm.Step);
        Assert.NotNull(_vm.SelectedProject);
    }

    // ---- JSON step ----

    [Fact]
    public async Task BrowseJson_SetsThePath()
    {
        await GoToJsonStepAsync();
        _picker.JsonFile = _jsonPath;

        await _vm.BrowseJsonCommand.ExecuteAsync(null);

        Assert.Equal(_jsonPath, _vm.JsonPath);
        Assert.True(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Analyze_NeedsAJsonPath()
    {
        await GoToJsonStepAsync();

        _vm.JsonPath = "   ";

        Assert.False(_vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Analyze_SendsTheRequestAndShowsTheConfirmStep()
    {
        await GoToJsonStepAsync();
        _vm.JsonPath = "  " + _jsonPath + "  ";

        await _vm.AnalyzeCommand.ExecuteAsync(null);

        ImportRequest request = Assert.Single(_service.AnalyzeCalls);
        Assert.Equal(_vm.SelectedProject!.Path, request.ProjectPath);
        Assert.Equal(Path.GetFullPath(_jsonPath), request.Json.DisplayName);
        Assert.Contains("new-secret", request.Json.Content);
        Assert.Equal(ImportMode.Overwrite, request.Mode);
        Assert.Null(request.Section);

        Assert.Equal(WizardStep.Confirm, _vm.Step);
        Assert.True(_vm.IsConfirmStep);
        Assert.NotNull(_vm.Analysis);
        Assert.Equal(["Db:Password", "Api:Key"], _vm.DiffRows.Select(static r => r.Key));
        Assert.All(_vm.DiffRows, static r => Assert.False(r.IsRevealed));
        Assert.True(_vm.ApplyCommand.CanExecute(null));
        Assert.False(_vm.HasNoChanges);
    }

    [Fact]
    public async Task Analyze_PassesATrimmedSection()
    {
        await GoToJsonStepAsync();
        _vm.JsonPath = _jsonPath;
        _vm.Section = "  Db  ";

        await _vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal("Db", Assert.Single(_service.AnalyzeCalls).Section);
    }

    [Fact]
    public async Task Analyze_MissingJsonFile_ShowsTheErrorOnTheJsonStep()
    {
        await GoToJsonStepAsync();
        _vm.JsonPath = Path.Combine(_temp.Path, "nope.json");

        await _vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Json, _vm.Step);
        Assert.Contains("not found", _vm.ErrorMessage);
        Assert.Empty(_service.AnalyzeCalls);
    }

    [Fact]
    public async Task Analyze_ParseError_ShowsTheErrorOnTheJsonStep()
    {
        _service.Analyze = static _ => throw new ImportException(ImportErrorKind.InvalidInput, "Duplicate key 'A' after flattening.");
        await GoToJsonStepAsync();
        _vm.JsonPath = _jsonPath;

        await _vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Json, _vm.Step);
        Assert.Equal("Duplicate key 'A' after flattening.", _vm.ErrorMessage);
        Assert.Null(_vm.Analysis);
    }

    // ---- Init step ----

    [Fact]
    public async Task Analyze_WhenNotInitialized_AsksToInit()
    {
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true);

        await GoToInitOrConfirmAsync();

        Assert.Equal(WizardStep.Init, _vm.Step);
        Assert.True(_vm.IsInitStep);
        Assert.True(_vm.CanInit);
        Assert.False(_vm.ShowManualInit);
        Assert.True(_vm.InitProjectCommand.CanExecute(null));
        Assert.Empty(_vm.DiffRows);
    }

    [Fact]
    public async Task Init_InitializesThenShowsTheConfirmStep()
    {
        int analyses = 0;
        _service.Analyze = r => TestData.Analysis(r, needsInit: analyses++ == 0);
        await GoToInitOrConfirmAsync();

        await _vm.InitProjectCommand.ExecuteAsync(null);

        Assert.Equal(_vm.SelectedProject!.Info.Project, Assert.Single(_service.InitCalls));
        Assert.Equal(2, _service.AnalyzeCalls.Count);
        Assert.Equal(WizardStep.Confirm, _vm.Step);
        Assert.Equal(2, _vm.DiffRows.Count);
    }

    [Fact]
    public async Task Init_WithoutSdk_IsDisabledAndShowsTheManualCommand()
    {
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true, canInit: false);

        await GoToInitOrConfirmAsync();

        Assert.False(_vm.CanInit);
        Assert.True(_vm.ShowManualInit);
        Assert.False(_vm.InitProjectCommand.CanExecute(null));
        Assert.Equal($"dotnet user-secrets init --project \"{_vm.SelectedProject!.Path}\"", _vm.ManualInitCommandText);
    }

    [Fact]
    public async Task Init_SdkUnavailable_ShowsTheManualCommand()
    {
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true);
        _service.Init = static _ => throw new ImportException(ImportErrorKind.SdkUnavailable, "The .NET SDK was not found.");
        await GoToInitOrConfirmAsync();

        await _vm.InitProjectCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Init, _vm.Step);
        Assert.True(_vm.SdkMissing);
        Assert.True(_vm.ShowManualInit);
        Assert.False(_vm.CanInit);
        Assert.False(_vm.InitProjectCommand.CanExecute(null));
        Assert.Equal("The .NET SDK was not found.", _vm.ErrorMessage);
        Assert.Single(_service.AnalyzeCalls);
    }

    [Fact]
    public async Task Init_OtherFailure_KeepsInitAvailable()
    {
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true);
        _service.Init = static _ => throw new ImportException(ImportErrorKind.IoFailure, "Cannot write the project file.");
        await GoToInitOrConfirmAsync();

        await _vm.InitProjectCommand.ExecuteAsync(null);

        Assert.False(_vm.SdkMissing);
        Assert.True(_vm.CanInit);
        Assert.Equal("Cannot write the project file.", _vm.ErrorMessage);
    }

    [Fact]
    public async Task CancelInit_GoesBackToTheJsonStep()
    {
        _service.Analyze = static r => TestData.Analysis(r, needsInit: true);
        _service.Init = static _ => throw new ImportException(ImportErrorKind.SdkUnavailable, "no sdk");
        await GoToInitOrConfirmAsync();
        await _vm.InitProjectCommand.ExecuteAsync(null);

        _vm.BackToJsonCommand.Execute(null);

        Assert.Equal(WizardStep.Json, _vm.Step);
        Assert.Null(_vm.Analysis);
        Assert.False(_vm.SdkMissing);
        Assert.False(_vm.HasError);
        Assert.Single(_service.InitCalls);
    }

    // ---- Confirm step ----

    [Fact]
    public async Task Confirm_ShowsWarningsAndNoChanges()
    {
        _service.Analyze = static r => TestData.Analysis(
            r,
            changes: [new SecretChange("A", SecretChangeKind.Unchanged, "x", "x")],
            warnings: [new ImportWarning(ImportWarningKind.SharedSecretsId, "Shared with B.")]);

        await GoToInitOrConfirmAsync();

        Assert.True(_vm.HasWarnings);
        Assert.True(_vm.HasNoChanges);
        Assert.False(_vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ChangingTheMode_OnConfirm_RecomputesThePlan()
    {
        await GoToInitOrConfirmAsync();
        DiffRowViewModel oldRow = _vm.DiffRows[0];
        oldRow.ToggleRevealCommand.Execute(null);
        _service.Analyze = static r => TestData.Analysis(r, changes: [new SecretChange("Only", SecretChangeKind.Removed, "gone", null)]);

        _vm.Mode = ImportMode.Replace;
        await _vm.ReanalyzeCommand.ExecutionTask!;

        Assert.Equal(ImportMode.Replace, _service.AnalyzeCalls[^1].Mode);
        Assert.Equal(2, _service.AnalyzeCalls.Count);
        Assert.Equal("Only", Assert.Single(_vm.DiffRows).Key);
        Assert.False(oldRow.IsRevealed);
        Assert.Equal(WizardStep.Confirm, _vm.Step);
    }

    [Fact]
    public async Task ChangingTheMode_BeforeConfirm_DoesNotAnalyze()
    {
        await GoToJsonStepAsync();

        _vm.Mode = ImportMode.Skip;

        Assert.Empty(_service.AnalyzeCalls);
    }

    [Fact]
    public async Task ChangingTheMode_WhileBusy_DoesNotAnalyzeAgain()
    {
        await GoToInitOrConfirmAsync();
        TaskCompletionSource gate = new();
        _service.Gate = gate.Task;
        Task apply = _vm.ApplyCommand.ExecuteAsync(null);

        _vm.Mode = ImportMode.Skip;
        gate.SetResult();
        await apply;

        Assert.Single(_service.AnalyzeCalls);
        Assert.Equal(WizardStep.Result, _vm.Step);
    }

    [Fact]
    public async Task LeavingConfirm_HidesAndDropsRevealedValues()
    {
        await GoToInitOrConfirmAsync();
        DiffRowViewModel row = _vm.DiffRows[0];
        row.ToggleRevealCommand.Execute(null);
        Assert.Equal("new-secret", row.NewValueDisplay);

        _vm.BackToJsonCommand.Execute(null);

        Assert.False(row.IsRevealed);
        Assert.Equal(DiffRowViewModel.Mask, row.NewValueDisplay);
        Assert.Empty(_vm.DiffRows);
        Assert.Null(_vm.Analysis);
        Assert.Equal(WizardStep.Json, _vm.Step);
    }

    [Fact]
    public async Task Apply_ShowsTheResultAndDropsThePlan()
    {
        _service.Apply = static a => new ImportResult(2, 1, 0, "path/secrets.json", "path/secrets.json.bak");
        await GoToInitOrConfirmAsync();
        ImportAnalysis analysis = _vm.Analysis!;
        DiffRowViewModel row = _vm.DiffRows[0];
        row.ToggleRevealCommand.Execute(null);

        await _vm.ApplyCommand.ExecuteAsync(null);

        Assert.Same(analysis, Assert.Single(_service.ApplyCalls));
        Assert.Equal(WizardStep.Result, _vm.Step);
        Assert.True(_vm.IsResultStep);
        Assert.Equal(new ImportResult(2, 1, 0, "path/secrets.json", "path/secrets.json.bak"), _vm.Result);
        Assert.Equal("path/secrets.json.bak", _vm.BackupPathDisplay);
        Assert.False(row.IsRevealed);
        Assert.Empty(_vm.DiffRows);
        Assert.Null(_vm.Analysis);
    }

    [Fact]
    public async Task Apply_WithoutBackup_SaysSo()
    {
        _service.Apply = static a => new ImportResult(2, 0, 0, "secrets.json", null);
        await GoToInitOrConfirmAsync();

        await _vm.ApplyCommand.ExecuteAsync(null);

        Assert.Contains("none", _vm.BackupPathDisplay);
    }

    [Fact]
    public async Task Apply_ConcurrentModification_StaysOnConfirmUntilReanalyzed()
    {
        _service.Apply = static _ => throw new ImportException(ImportErrorKind.ConcurrentModification, "secrets.json changed since it was analyzed.");
        await GoToInitOrConfirmAsync();

        await _vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(WizardStep.Confirm, _vm.Step);
        Assert.Equal("secrets.json changed since it was analyzed.", _vm.ErrorMessage);
        Assert.NotNull(_vm.Analysis);

        await _vm.ReanalyzeCommand.ExecuteAsync(null);

        Assert.Equal(2, _service.AnalyzeCalls.Count);
        Assert.False(_vm.HasError);
        Assert.Equal(WizardStep.Confirm, _vm.Step);
    }

    // ---- Result step ----

    [Fact]
    public async Task StartOver_ResetsTheWizard()
    {
        await GoToInitOrConfirmAsync();
        _vm.Mode = ImportMode.Skip;
        await _vm.ReanalyzeCommand.ExecutionTask!;
        _vm.Section = "Db";
        await _vm.ApplyCommand.ExecuteAsync(null);

        _vm.StartOverCommand.Execute(null);

        Assert.Equal(WizardStep.Project, _vm.Step);
        Assert.Null(_vm.Result);
        Assert.Empty(_vm.Projects);
        Assert.Null(_vm.SelectedProject);
        Assert.Equal(string.Empty, _vm.ProjectPath);
        Assert.Equal(string.Empty, _vm.JsonPath);
        Assert.Equal(string.Empty, _vm.Section);
        Assert.Equal(ImportMode.Overwrite, _vm.Mode);
        Assert.False(_vm.ReanalyzeCommand.CanExecute(null));
        Assert.Equal(3, _vm.RecentProjects.Count);
    }

    // ---- Busy state ----

    [Fact]
    public async Task WhileBusy_CommandsAreDisabled()
    {
        TaskCompletionSource gate = new();
        _service.Gate = gate.Task;
        _service.Discover = static _ => [TestData.Project("A")];
        _vm.ProjectPath = "x";
        List<string?> raised = [];
        _vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Task discover = _vm.DiscoverCommand.ExecuteAsync(null);

        Assert.True(_vm.IsBusy);
        Assert.False(_vm.IsIdle);
        Assert.False(_vm.BrowseProjectFileCommand.CanExecute(null));
        Assert.False(_vm.BrowseProjectFolderCommand.CanExecute(null));
        Assert.False(_vm.OpenRecentCommand.CanExecute(null));
        Assert.False(_vm.BrowseJsonCommand.CanExecute(null));
        Assert.Contains(nameof(MainWindowViewModel.IsIdle), raised);

        gate.SetResult();
        await discover;

        Assert.False(_vm.IsBusy);
        Assert.True(_vm.DiscoverCommand.CanExecute(null));
        Assert.True(_vm.ContinueToJsonCommand.CanExecute(null));
    }

    private async Task GoToJsonStepAsync()
    {
        _service.Discover = static _ => [TestData.Project("App")];
        _vm.ProjectPath = "App";
        await _vm.DiscoverCommand.ExecuteAsync(null);
        _vm.ContinueToJsonCommand.Execute(null);
    }

    private async Task GoToInitOrConfirmAsync()
    {
        await GoToJsonStepAsync();
        _vm.JsonPath = _jsonPath;
        await _vm.AnalyzeCommand.ExecuteAsync(null);
    }
}
