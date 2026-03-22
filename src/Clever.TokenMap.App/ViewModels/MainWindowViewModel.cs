using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly string _windowTitle = "TokenMap";
    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly TreemapNavigationState _treemapNavigationState;
    private readonly RelayCommand<ProjectNode?> _navigateToTreemapBreadcrumbCommand;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly RelayCommand _toggleSettingsCommand;

    [ObservableProperty]
    private bool isSettingsOpen;

    public MainWindowViewModel()
        : this(
            CreateAnalysisSessionController(new NullProjectAnalyzer(), new NullFolderPickerService(), NullAppLogger.Instance),
            new TreemapNavigationState(),
            new NullSettingsCoordinator())
    {
    }

    public MainWindowViewModel(
        IAnalysisSessionController analysisSessionController,
        TreemapNavigationState treemapNavigationState,
        ISettingsCoordinator settingsCoordinator)
    {
        _analysisSessionController = analysisSessionController;
        _treemapNavigationState = treemapNavigationState;
        _settingsCoordinator = settingsCoordinator;

        Toolbar = new ToolbarViewModel(
            new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder),
            new AsyncRelayCommand(RescanAsync, CanRescan),
            new RelayCommand(CancelAnalysis, CanCancel));
        Tree = new ProjectTreeViewModel();
        Summary = new SummaryViewModel();

        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(NavigateToTreemapBreadcrumb);
        _resetTreemapRootCommand = new RelayCommand(ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _treemapNavigationState.PropertyChanged += TreemapNavigationStateOnPropertyChanged;

        _settingsCoordinator.Attach(Toolbar);
        Toolbar.UpdateFolder(_analysisSessionController.SelectedFolderPath);
        RefreshToolbarAvailability();

        if (_analysisSessionController.CurrentSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }

        Summary.SetState(_analysisSessionController.State);
        if (_analysisSessionController.CurrentProgress is { } progress)
        {
            Summary.UpdateProgress(progress);
        }
    }

    public string WindowTitle => _windowTitle;

    public ToolbarViewModel Toolbar { get; }

    public ProjectTreeViewModel Tree { get; }

    public SummaryViewModel Summary { get; }

    public ProjectNode? TreemapRootNode => _treemapNavigationState.TreemapRootNode;

    public ProjectNode? SelectedNode
    {
        get => _treemapNavigationState.SelectedNode;
        set => _treemapNavigationState.SelectNode(value);
    }

    public AnalysisState AnalysisState => _analysisSessionController.State;

    public IReadOnlyList<TreemapBreadcrumbItemViewModel> TreemapBreadcrumbs => _treemapNavigationState.TreemapBreadcrumbs;

    public bool CanResetTreemapRoot => _treemapNavigationState.CanResetTreemapRoot;

    public bool CanShowTreemapScope => _treemapNavigationState.CanShowTreemapScope;

    public string TreemapScopeDisplay => _treemapNavigationState.TreemapScopeDisplay;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public IRelayCommand<ProjectNode?> NavigateToTreemapBreadcrumbCommand => _navigateToTreemapBreadcrumbCommand;

    private bool CanOpenFolder() => !_analysisSessionController.IsBusy;

    private bool CanRescan() => !_analysisSessionController.IsBusy && _analysisSessionController.HasSelectedFolder;

    private bool CanCancel() => _analysisSessionController.IsBusy;

    public void DrillIntoTreemap(ProjectNode? node)
    {
        _treemapNavigationState.DrillInto(node);
    }

    private async Task OpenFolderAsync()
    {
        await _analysisSessionController.OpenFolderAsync(Toolbar.BuildScanOptions());
    }

    private async Task RescanAsync()
    {
        await _analysisSessionController.RescanAsync(Toolbar.BuildScanOptions());
    }

    private void CancelAnalysis()
    {
        _analysisSessionController.Cancel();
    }

    private void ResetTreemapRoot()
    {
        _treemapNavigationState.ResetTreemapRoot();
    }

    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    private void NavigateToTreemapBreadcrumb(ProjectNode? node)
    {
        _treemapNavigationState.NavigateToBreadcrumb(node);
    }

    private void AnalysisSessionControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnalysisSessionController.SelectedFolderPath):
                Toolbar.UpdateFolder(_analysisSessionController.SelectedFolderPath);
                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.CurrentSnapshot):
                if (_analysisSessionController.CurrentSnapshot is { } snapshot)
                {
                    ApplySnapshot(snapshot);
                }
                else
                {
                    Tree.Clear();
                    _treemapNavigationState.Clear();
                }

                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.State):
                OnPropertyChanged(nameof(AnalysisState));
                Summary.SetState(_analysisSessionController.State);
                if (_analysisSessionController.State == AnalysisState.Completed &&
                    _analysisSessionController.CurrentSnapshot is { } completedSnapshot)
                {
                    Summary.SetCompleted(completedSnapshot);
                }

                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.StatusMessage):
                Summary.SetState(_analysisSessionController.State);
                break;
            case nameof(IAnalysisSessionController.CurrentProgress):
                if (_analysisSessionController.CurrentProgress is { } progress)
                {
                    Summary.UpdateProgress(progress);
                }

                break;
        }
    }

    private void TreemapNavigationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TreemapNavigationState.SelectedNode):
                OnPropertyChanged(nameof(SelectedNode));
                Tree.SelectNodeById(_treemapNavigationState.SelectedNode?.Id);
                break;
            case nameof(TreemapNavigationState.TreemapRootNode):
                OnPropertyChanged(nameof(TreemapRootNode));
                OnPropertyChanged(nameof(CanResetTreemapRoot));
                OnPropertyChanged(nameof(CanShowTreemapScope));
                OnPropertyChanged(nameof(TreemapScopeDisplay));
                _resetTreemapRootCommand.NotifyCanExecuteChanged();
                break;
            case nameof(TreemapNavigationState.TreemapBreadcrumbs):
                OnPropertyChanged(nameof(TreemapBreadcrumbs));
                break;
        }
    }

    private void ApplySnapshot(ProjectSnapshot snapshot)
    {
        Tree.LoadRoot(new ProjectTreeNodeViewModel(snapshot.Root));
        _treemapNavigationState.LoadSnapshot(snapshot);
        Summary.SetCompleted(snapshot);
    }

    private void RefreshToolbarAvailability()
    {
        Toolbar.RefreshAvailability(
            _analysisSessionController.IsBusy,
            _analysisSessionController.HasSnapshot);
    }

    private static AnalysisSessionController CreateAnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IAppLogger? logger)
    {
        ArgumentNullException.ThrowIfNull(projectAnalyzer);
        ArgumentNullException.ThrowIfNull(folderPickerService);

        return new AnalysisSessionController(projectAnalyzer, folderPickerService, logger);
    }

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullProjectAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Project analyzer is not configured.");
    }

    private sealed class NullSettingsCoordinator : ISettingsCoordinator
    {
        public void Attach(ToolbarViewModel toolbar)
        {
        }
    }
}
