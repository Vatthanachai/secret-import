using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SecretImport.Core;
using SecretImport.Desktop.Services;

namespace SecretImport.Desktop.ViewModels;

/// <summary>
/// The import wizard: Project → JSON → (Init) → Confirm → Result. All work goes through <see cref="IImportService"/>.
/// Secret values live only in the diff rows of the Confirm step and are dropped as soon as the wizard leaves it.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IImportService _importService;
    private readonly IFilePickerService _pickers;
    private readonly IRecentProjectsStore _recentProjects;
    private readonly SolutionScopeResolver _scopeResolver;
    private JsonInput? _json;

    /// <summary>
    /// The <see cref="ImportRequest.ScopePath"/> for the listed projects (ADR-0013 Amendment 1): the folder or solution they were
    /// discovered from, or, for a single project file, the nearest solution <see cref="SolutionScopeResolver"/> finds (null if none).
    /// </summary>
    private string? _scopePath;

    public MainWindowViewModel(
        IImportService importService, IFilePickerService pickers, IRecentProjectsStore recentProjects, SolutionScopeResolver scopeResolver)
    {
        _scopeResolver = scopeResolver;
        _importService = importService;
        _pickers = pickers;
        _recentProjects = recentProjects;
        RecentProjects = new ObservableCollection<string>(recentProjects.Load());
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "Secret Import";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectStep), nameof(IsJsonStep), nameof(IsInitStep), nameof(IsConfirmStep), nameof(IsResultStep))]
    public partial WizardStep Step { get; private set; } = WizardStep.Project;

    public bool IsProjectStep => Step == WizardStep.Project;

    public bool IsJsonStep => Step == WizardStep.Json;

    public bool IsInitStep => Step == WizardStep.Init;

    public bool IsConfirmStep => Step == WizardStep.Confirm;

    public bool IsResultStep => Step == WizardStep.Result;

    /// <summary>The error of the current step, shown inline. Never contains a secret value.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(BrowseProjectFileCommand), nameof(BrowseProjectFolderCommand), nameof(DiscoverCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRecentCommand), nameof(ContinueToJsonCommand), nameof(BrowseJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(InitProjectCommand), nameof(ReanalyzeCommand), nameof(ApplyCommand))]
    public partial bool IsBusy { get; private set; }

    public bool IsIdle => !IsBusy;

    // ---- Project step ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DiscoverCommand))]
    public partial string ProjectPath { get; set; } = string.Empty;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    /// <summary>Problems found while reading the folder or solution, e.g. a project listed in the solution that does not exist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscoveryWarnings))]
    public partial IReadOnlyList<ImportWarning> DiscoveryWarnings { get; private set; } = [];

    public bool HasDiscoveryWarnings => DiscoveryWarnings.Count > 0;

    public ObservableCollection<string> RecentProjects { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManualInitCommandText))]
    [NotifyCanExecuteChangedFor(nameof(ContinueToJsonCommand), nameof(AnalyzeCommand))]
    public partial ProjectItemViewModel? SelectedProject { get; set; }

    // ---- JSON step ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial string JsonPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Section { get; set; } = string.Empty;

    // ---- Init step ----

    /// <summary>Set when <see cref="IImportService.InitAsync"/> reported that the .NET SDK is missing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInit), nameof(ShowManualInit))]
    [NotifyCanExecuteChangedFor(nameof(InitProjectCommand))]
    public partial bool SdkMissing { get; private set; }

    public bool CanInit => Analysis?.CanInit == true && !SdkMissing;

    public bool ShowManualInit => !CanInit;

    public string ManualInitCommandText => $"dotnet user-secrets init --project \"{SelectedProject?.Path}\"";

    // ---- Confirm step ----

    /// <summary>The analysis shown on the Init or Confirm step; <see langword="null"/> on every other step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInit), nameof(ShowManualInit), nameof(HasWarnings), nameof(HasNoChanges))]
    [NotifyCanExecuteChangedFor(nameof(InitProjectCommand), nameof(ApplyCommand))]
    public partial ImportAnalysis? Analysis { get; private set; }

    public IReadOnlyList<ImportMode> Modes { get; } = Enum.GetValues<ImportMode>();

    [ObservableProperty]
    public partial ImportMode Mode { get; set; } = ImportMode.Overwrite;

    public ObservableCollection<DiffRowViewModel> DiffRows { get; } = [];

    public bool HasWarnings => Analysis?.Warnings.Count > 0;

    public bool HasNoChanges => Analysis?.Plan.HasChanges == false;

    // ---- Result step ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupPathDisplay))]
    public partial ImportResult? Result { get; private set; }

    public string BackupPathDisplay => Result?.BackupPath ?? "(none: there was no secrets.json to back up)";

    // ---- Commands ----

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task BrowseProjectFileAsync()
    {
        return RunAsync(PickProjectFileAndDiscoverAsync);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task BrowseProjectFolderAsync()
    {
        return RunAsync(PickFolderAndDiscoverAsync);
    }

    [RelayCommand(CanExecute = nameof(CanDiscover))]
    private Task DiscoverAsync()
    {
        return RunAsync(DiscoverProjectsAsync);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task OpenRecentAsync(string? path)
    {
        return RunAsync(() => DiscoverPickedAsync(path));
    }

    [RelayCommand(CanExecute = nameof(CanContinueToJson))]
    private void ContinueToJson()
    {
        ReplaceRecentProjects(_recentProjects.Add(SelectedProject!.Path));
        GoTo(WizardStep.Json);
    }

    [RelayCommand]
    private void BackToProject()
    {
        GoTo(WizardStep.Project);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task BrowseJsonAsync()
    {
        return RunAsync(PickJsonFileAsync);
    }

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private Task AnalyzeAsync()
    {
        return RunAsync(ReadJsonAndAnalyzeAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunInit))]
    private Task InitProjectAsync()
    {
        return RunAsync(InitAndAnalyzeAsync);
    }

    [RelayCommand(CanExecute = nameof(CanReanalyze))]
    private Task ReanalyzeAsync()
    {
        return RunAsync(AnalyzeCurrentAsync);
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync()
    {
        return RunAsync(ApplyAndShowResultAsync);
    }

    /// <summary>Leaves the Init or Confirm step and goes back to the JSON step.</summary>
    [RelayCommand]
    private void BackToJson()
    {
        GoTo(WizardStep.Json);
    }

    [RelayCommand]
    private void StartOver()
    {
        GoTo(WizardStep.Project);
        Result = null;
        _json = null;
        _scopePath = null;
        DiscoveryWarnings = [];
        Projects.Clear();
        SelectedProject = null;
        ProjectPath = string.Empty;
        JsonPath = string.Empty;
        Section = string.Empty;
        Mode = ImportMode.Overwrite;
    }

    private bool CanDiscover()
    {
        return IsIdle && !string.IsNullOrWhiteSpace(ProjectPath);
    }

    private bool CanContinueToJson()
    {
        return IsIdle && SelectedProject is not null;
    }

    private bool CanAnalyze()
    {
        return IsIdle && SelectedProject is not null && !string.IsNullOrWhiteSpace(JsonPath);
    }

    private bool CanRunInit()
    {
        return IsIdle && CanInit;
    }

    private bool CanReanalyze()
    {
        return IsIdle && _json is not null && SelectedProject is not null;
    }

    private bool CanApply()
    {
        return IsIdle && Analysis?.Plan.HasChanges == true;
    }

    partial void OnModeChanged(ImportMode value)
    {
        if (Step == WizardStep.Confirm && ReanalyzeCommand.CanExecute(null))
        {
            ReanalyzeCommand.Execute(null);
        }
    }

    private async Task PickProjectFileAndDiscoverAsync()
    {
        await DiscoverPickedAsync(await _pickers.PickProjectFileAsync());
    }

    private async Task PickFolderAndDiscoverAsync()
    {
        await DiscoverPickedAsync(await _pickers.PickFolderAsync());
    }

    private async Task PickJsonFileAsync()
    {
        string? path = await _pickers.PickJsonFileAsync();
        if (path is not null)
        {
            JsonPath = path;
        }
    }

    private async Task ReadJsonAndAnalyzeAsync()
    {
        _json = JsonInput.FromFile(JsonPath.Trim());
        await AnalyzeCurrentAsync();
    }

    private async Task InitAndAnalyzeAsync()
    {
        try
        {
            await _importService.InitAsync(SelectedProject!.Info.Project);
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.SdkUnavailable)
        {
            SdkMissing = true;
            throw;
        }

        await AnalyzeCurrentAsync();
    }

    private async Task ApplyAndShowResultAsync()
    {
        ImportResult result = await _importService.ApplyAsync(Analysis!);
        GoTo(WizardStep.Result);
        Result = result;
    }

    private async Task DiscoverPickedAsync(string? path)
    {
        if (path is null)
        {
            return;
        }

        ProjectPath = path;
        await DiscoverProjectsAsync();
    }

    private async Task DiscoverProjectsAsync()
    {
        Projects.Clear();
        SelectedProject = null;
        DiscoveryWarnings = [];
        _scopePath = null;
        string path = ProjectPath.Trim();
        DiscoveryResult found = await _importService.DiscoverProjectsAsync(path);
        _scopePath = SolutionScopeResolver.IsProjectFile(path) ? _scopeResolver.Resolve(path, null) : path;
        DiscoveryWarnings = found.Warnings;
        foreach (ProjectInfo info in found.Projects)
        {
            Projects.Add(new ProjectItemViewModel(info));
        }

        if (Projects.Count == 0)
        {
            ErrorMessage = "No .csproj, .fsproj or .vbproj project was found there.";
        }
        else if (Projects.Count == 1)
        {
            SelectedProject = Projects[0];
        }
    }

    private async Task AnalyzeCurrentAsync()
    {
        string? section = string.IsNullOrWhiteSpace(Section) ? null : Section.Trim();
        ImportRequest request = new(SelectedProject!.Path, _json!, Mode, section, _scopePath);
        ImportAnalysis analysis = await _importService.AnalyzeAsync(request);

        ClearPlan();
        Analysis = analysis;
        if (analysis.NeedsInit)
        {
            Step = WizardStep.Init;
            return;
        }

        foreach (SecretChange change in analysis.Plan.Changes)
        {
            DiffRows.Add(new DiffRowViewModel(change));
        }

        Step = WizardStep.Confirm;
    }

    private void GoTo(WizardStep step)
    {
        ClearPlan();
        ErrorMessage = null;
        SdkMissing = false;
        Step = step;
    }

    /// <summary>Hides and drops every diff row and the analysis, so no revealed secret value outlives the Confirm step.</summary>
    private void ClearPlan()
    {
        foreach (DiffRowViewModel row in DiffRows)
        {
            row.IsRevealed = false;
        }

        DiffRows.Clear();
        Analysis = null;
    }

    private void ReplaceRecentProjects(IReadOnlyList<string> paths)
    {
        RecentProjects.Clear();
        foreach (string path in paths)
        {
            RecentProjects.Add(path);
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            // Core exception messages never contain secret values (see the ImportException / ImportWarning contracts).
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
