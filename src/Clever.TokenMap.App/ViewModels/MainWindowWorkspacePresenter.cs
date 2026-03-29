using System.Collections.Generic;
using System.ComponentModel;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public sealed class MainWindowWorkspacePresenter : ObservableObject
{
    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly ISummaryProjection _summary;
    private readonly IToolbarAvailabilitySink _toolbar;
    private readonly TreemapNavigationState _treemapNavigationState;
    private readonly IProjectTreeWorkspaceView _tree;

    public MainWindowWorkspacePresenter(
        IAnalysisSessionController analysisSessionController,
        TreemapNavigationState treemapNavigationState,
        ISettingsCoordinator settingsCoordinator,
        IToolbarAvailabilitySink toolbar,
        IProjectTreeWorkspaceView tree,
        ISummaryProjection summary)
    {
        _analysisSessionController = analysisSessionController;
        _treemapNavigationState = treemapNavigationState;
        _settingsCoordinator = settingsCoordinator;
        _toolbar = toolbar;
        _tree = tree;
        _summary = summary;

        _tree.SetShareMetric(_settingsCoordinator.State.SelectedMetric);
        _tree.SelectedNodeChanged += TreeOnSelectedNodeChanged;
        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _settingsCoordinator.State.PropertyChanged += SettingsStateOnPropertyChanged;
        _treemapNavigationState.PropertyChanged += TreemapNavigationStateOnPropertyChanged;

        _settingsCoordinator.SwitchActiveFolder(_analysisSessionController.SelectedFolderPath);
        RefreshToolbarAvailability();

        if (_analysisSessionController.CurrentSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }

        _summary.SetState(_analysisSessionController.State);
        if (_analysisSessionController.CurrentProgress is { } progress)
        {
            _summary.UpdateProgress(progress);
        }
    }

    public string WindowTitle => BuildWindowTitle(_analysisSessionController.SelectedFolderPath);

    public string ProjectTreeSelectedFolderText => _analysisSessionController.SelectedFolderPath?.Trim() ?? string.Empty;

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

    public void DrillIntoTreemap(ProjectNode? node)
    {
        _treemapNavigationState.DrillInto(node);
    }

    public bool CanSetTreemapRoot(ProjectNode? node) => _treemapNavigationState.CanSetTreemapRoot(node);

    public void SetTreemapRoot(ProjectNode? node) => _treemapNavigationState.SetTreemapRoot(node);

    public void ResetTreemapRoot()
    {
        _treemapNavigationState.ResetTreemapRoot();
    }

    public void NavigateToTreemapBreadcrumb(ProjectNode? node)
    {
        _treemapNavigationState.NavigateToBreadcrumb(node);
    }

    public ShareSnapshotViewModel? CreateShareSnapshotViewModel()
    {
        if (_analysisSessionController.CurrentSnapshot is not { } snapshot)
        {
            return null;
        }

        return new ShareSnapshotViewModel(
            snapshot,
            FolderDisplayText.GetFolderDisplayName(_analysisSessionController.SelectedFolderPath));
    }

    private void TreeOnSelectedNodeChanged(object? sender, ProjectNode? node)
    {
        SelectedNode = node;
    }

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
                    _tree.Clear();
                    _treemapNavigationState.Clear();
                }

                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.State):
                OnPropertyChanged(nameof(AnalysisState));
                _summary.SetState(_analysisSessionController.State);
                if (_analysisSessionController.State == AnalysisState.Completed &&
                    _analysisSessionController.CurrentSnapshot is { } completedSnapshot)
                {
                    _summary.SetCompleted(completedSnapshot);
                }

                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.CurrentProgress):
                if (_analysisSessionController.CurrentProgress is { } progress)
                {
                    _summary.UpdateProgress(progress);
                }

                break;
        }
    }

    private void SettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlySettingsState.SelectedMetric))
        {
            _tree.SetShareMetric(_settingsCoordinator.State.SelectedMetric);
        }
    }

    private void TreemapNavigationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TreemapNavigationState.SelectedNode):
                OnPropertyChanged(nameof(SelectedNode));
                _tree.SelectNodeById(_treemapNavigationState.SelectedNode?.Id);
                break;
            case nameof(TreemapNavigationState.TreemapRootNode):
                OnPropertyChanged(nameof(TreemapRootNode));
                OnPropertyChanged(nameof(CanResetTreemapRoot));
                break;
            case nameof(TreemapNavigationState.TreemapBreadcrumbs):
                OnPropertyChanged(nameof(TreemapBreadcrumbs));
                break;
        }
    }

    private void ApplySnapshot(ProjectSnapshot snapshot)
    {
        _tree.LoadRoot(snapshot.Root);
        _treemapNavigationState.LoadSnapshot(snapshot);
        _summary.SetCompleted(snapshot);
    }

    private void RefreshToolbarAvailability()
    {
        _toolbar.RefreshAvailability(
            _analysisSessionController.IsBusy,
            _analysisSessionController.HasSnapshot);
    }

    private static string BuildWindowTitle(string? folderPath)
    {
        var displayName = FolderDisplayText.GetFolderDisplayName(folderPath);
        return string.IsNullOrWhiteSpace(displayName)
            ? "TokenMap"
            : $"{displayName} - TokenMap";
    }
}
