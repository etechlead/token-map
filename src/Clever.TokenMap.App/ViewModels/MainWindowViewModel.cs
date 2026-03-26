using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly IPathShellService _pathShellService;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly TreemapNavigationState _treemapNavigationState;
    private readonly RelayCommand _closeSettingsCommand;
    private readonly RelayCommand<ProjectNode?> _navigateToTreemapBreadcrumbCommand;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly RelayCommand _toggleSettingsCommand;

    [ObservableProperty]
    private bool isSettingsOpen;

    public MainWindowViewModel(
        IAnalysisSessionController analysisSessionController,
        TreemapNavigationState treemapNavigationState,
        ISettingsCoordinator settingsCoordinator,
        IFolderPathService folderPathService,
        IPathShellService pathShellService)
    {
        _analysisSessionController = analysisSessionController;
        _treemapNavigationState = treemapNavigationState;
        _settingsCoordinator = settingsCoordinator;
        _pathShellService = pathShellService;

        Toolbar = new ToolbarViewModel(
            _settingsCoordinator,
            new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder),
            new AsyncRelayCommand(RescanAsync, CanRescan),
            new RelayCommand(CancelAnalysis, CanCancel));
        ExcludesEditor = new ExcludesEditorViewModel(
            _settingsCoordinator,
            _analysisSessionController,
            Toolbar.BuildScanOptions);
        RecentFolders = new RecentFoldersViewModel(
            _analysisSessionController,
            _settingsCoordinator,
            folderPathService,
            Toolbar.BuildScanOptions);
        Tree = new ProjectTreeViewModel();
        Tree.SetShareMetric(_settingsCoordinator.State.SelectedMetric);
        Summary = new SummaryViewModel();

        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(NavigateToTreemapBreadcrumb);
        _closeSettingsCommand = new RelayCommand(CloseSettings);
        _resetTreemapRootCommand = new RelayCommand(ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _settingsCoordinator.State.PropertyChanged += SettingsStateOnPropertyChanged;
        _treemapNavigationState.PropertyChanged += TreemapNavigationStateOnPropertyChanged;

        _settingsCoordinator.SwitchActiveFolder(_analysisSessionController.SelectedFolderPath);
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

    public string WindowTitle => BuildWindowTitle(_analysisSessionController.SelectedFolderPath);

    public string ProjectTreeSelectedFolderText => _analysisSessionController.SelectedFolderPath?.Trim() ?? string.Empty;

    public ToolbarViewModel Toolbar { get; }

    public ExcludesEditorViewModel ExcludesEditor { get; }

    public RecentFoldersViewModel RecentFolders { get; }

    public ProjectTreeViewModel Tree { get; }

    public SummaryViewModel Summary { get; }

    public string RevealMenuHeader => _pathShellService.RevealMenuHeader;

    public ProjectNode? TreemapRootNode => _treemapNavigationState.TreemapRootNode;

    public bool HasSnapshot => _analysisSessionController.HasSnapshot;

    public ProjectNode? SelectedNode
    {
        get => _treemapNavigationState.SelectedNode;
        set => _treemapNavigationState.SelectNode(value);
    }

    public AnalysisState AnalysisState => _analysisSessionController.State;

    public IReadOnlyList<TreemapBreadcrumbItem> TreemapBreadcrumbs => _treemapNavigationState.TreemapBreadcrumbs;

    public bool CanResetTreemapRoot => _treemapNavigationState.CanResetTreemapRoot;

    public bool CanShowTreemapScope => _treemapNavigationState.CanShowTreemapScope;

    public string TreemapScopeDisplay => _treemapNavigationState.TreemapScopeDisplay;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public IRelayCommand CloseSettingsCommand => _closeSettingsCommand;

    public IRelayCommand<ProjectNode?> ExcludeNodeFromFolderCommand => ExcludesEditor.ExcludeNodeFromFolderCommand;

    public IRelayCommand OpenGlobalExcludesEditorCommand => ExcludesEditor.OpenGlobalCommand;

    public IRelayCommand OpenFolderExcludesEditorCommand => ExcludesEditor.OpenFolderCommand;

    public IRelayCommand CancelExcludesEditorCommand => ExcludesEditor.CancelCommand;

    public IRelayCommand SaveExcludesEditorCommand => ExcludesEditor.SaveCommand;

    public IAsyncRelayCommand SaveAndRescanExcludesEditorCommand => ExcludesEditor.SaveAndRescanCommand;

    public IRelayCommand<ProjectNode?> NavigateToTreemapBreadcrumbCommand => _navigateToTreemapBreadcrumbCommand;

    private bool CanOpenFolder() => !_analysisSessionController.IsBusy;

    private bool CanRescan() => !_analysisSessionController.IsBusy && _analysisSessionController.HasSelectedFolder;

    private bool CanCancel() => _analysisSessionController.IsBusy;

    public void DrillIntoTreemap(ProjectNode? node)
    {
        _treemapNavigationState.DrillInto(node);
    }

    public bool CanSetTreemapRoot(ProjectNode? node) => _treemapNavigationState.CanSetTreemapRoot(node);

    public void SetTreemapRoot(ProjectNode? node) => _treemapNavigationState.SetTreemapRoot(node);

    public Task OpenNodeAsync(ProjectNode? node, CancellationToken cancellationToken = default)
    {
        if (node is null)
        {
            return Task.CompletedTask;
        }

        return _pathShellService.TryOpenAsync(node.FullPath, cancellationToken);
    }

    public Task RevealNodeAsync(ProjectNode? node, CancellationToken cancellationToken = default)
    {
        if (node is null)
        {
            return Task.CompletedTask;
        }

        return _pathShellService.TryRevealAsync(
            node.FullPath,
            node.Kind is not Core.Enums.ProjectNodeKind.File,
            cancellationToken);
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

    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    private void NavigateToTreemapBreadcrumb(ProjectNode? node)
    {
        _treemapNavigationState.NavigateToBreadcrumb(node);
    }

    public bool CanExcludeNodeFromFolder(ProjectNode? node) => ExcludesEditor.CanExcludeNodeFromFolder(node);

    private void AnalysisSessionControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnalysisSessionController.SelectedFolderPath):
                _settingsCoordinator.SwitchActiveFolder(_analysisSessionController.SelectedFolderPath);
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ProjectTreeSelectedFolderText));
                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.CurrentSnapshot):
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(HasSnapshot));
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

    private void SettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsState.SelectedMetric))
        {
            Tree.SetShareMetric(_settingsCoordinator.State.SelectedMetric);
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
        Tree.LoadRoot(snapshot.Root);
        _treemapNavigationState.LoadSnapshot(snapshot);
        Summary.SetCompleted(snapshot);
    }

    private void RefreshToolbarAvailability()
    {
        Toolbar.RefreshAvailability(
            _analysisSessionController.IsBusy,
            _analysisSessionController.HasSnapshot);
    }

    private static string BuildWindowTitle(string? folderPath)
    {
        var displayName = GetFolderDisplayName(folderPath);
        return string.IsNullOrWhiteSpace(displayName)
            ? "TokenMap"
            : $"{displayName} - TokenMap";
    }

    private static string GetFolderDisplayName(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        var trimmedPath = folderPath.Trim();
        var displayName = Path.GetFileName(trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(displayName)
            ? trimmedPath
            : displayName;
    }
}
