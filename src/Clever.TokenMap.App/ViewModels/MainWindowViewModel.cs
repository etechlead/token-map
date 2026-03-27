using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPathShellService _pathShellService;
    private readonly MainWindowWorkspacePresenter _workspacePresenter;
    private readonly RelayCommand _closeSettingsCommand;
    private readonly RelayCommand _closeShareSnapshotCommand;
    private readonly RelayCommand<ProjectNode?> _navigateToTreemapBreadcrumbCommand;
    private readonly RelayCommand _openShareSnapshotCommand;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly RelayCommand _toggleSettingsCommand;

    [ObservableProperty]
    private bool isSettingsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShareSnapshotOpen))]
    private ShareSnapshotViewModel? shareSnapshot;

    public MainWindowViewModel(
        MainWindowWorkspacePresenter workspacePresenter,
        ToolbarViewModel toolbar,
        ExcludesEditorViewModel excludesEditor,
        RecentFoldersViewModel recentFolders,
        ProjectTreeViewModel tree,
        SummaryViewModel summary,
        IPathShellService pathShellService)
    {
        _workspacePresenter = workspacePresenter;
        _pathShellService = pathShellService;

        Toolbar = toolbar;
        ExcludesEditor = excludesEditor;
        RecentFolders = recentFolders;
        Tree = tree;
        Summary = summary;

        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(_workspacePresenter.NavigateToTreemapBreadcrumb);
        _closeSettingsCommand = new RelayCommand(CloseSettings);
        _closeShareSnapshotCommand = new RelayCommand(CloseShareSnapshot);
        _openShareSnapshotCommand = new RelayCommand(OpenShareSnapshot, () => HasSnapshot);
        _resetTreemapRootCommand = new RelayCommand(_workspacePresenter.ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);

        _workspacePresenter.PropertyChanged += WorkspacePresenterOnPropertyChanged;
    }

    public string WindowTitle => _workspacePresenter.WindowTitle;

    public string ProjectTreeSelectedFolderText => _workspacePresenter.ProjectTreeSelectedFolderText;

    public ToolbarViewModel Toolbar { get; }

    public ExcludesEditorViewModel ExcludesEditor { get; }

    public RecentFoldersViewModel RecentFolders { get; }

    public ProjectTreeViewModel Tree { get; }

    public SummaryViewModel Summary { get; }

    public string RevealMenuHeader => _pathShellService.RevealMenuHeader;

    public ProjectNode? TreemapRootNode => _workspacePresenter.TreemapRootNode;

    public bool HasSnapshot => _workspacePresenter.HasSnapshot;

    public ProjectNode? SelectedNode
    {
        get => _workspacePresenter.SelectedNode;
        set => _workspacePresenter.SelectedNode = value;
    }

    public AnalysisState AnalysisState => _workspacePresenter.AnalysisState;

    public IReadOnlyList<TreemapBreadcrumbItem> TreemapBreadcrumbs => _workspacePresenter.TreemapBreadcrumbs;

    public bool CanResetTreemapRoot => _workspacePresenter.CanResetTreemapRoot;

    public bool IsShareSnapshotOpen => ShareSnapshot is not null;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public IRelayCommand CloseSettingsCommand => _closeSettingsCommand;

    public IRelayCommand OpenShareSnapshotCommand => _openShareSnapshotCommand;

    public IRelayCommand CloseShareSnapshotCommand => _closeShareSnapshotCommand;

    public IRelayCommand<ProjectNode?> ExcludeNodeFromFolderCommand => ExcludesEditor.ExcludeNodeFromFolderCommand;

    public IRelayCommand OpenGlobalExcludesEditorCommand => ExcludesEditor.OpenGlobalCommand;

    public IRelayCommand OpenFolderExcludesEditorCommand => ExcludesEditor.OpenFolderCommand;

    public IRelayCommand CancelExcludesEditorCommand => ExcludesEditor.CancelCommand;

    public IRelayCommand SaveExcludesEditorCommand => ExcludesEditor.SaveCommand;

    public IAsyncRelayCommand SaveAndRescanExcludesEditorCommand => ExcludesEditor.SaveAndRescanCommand;

    public IRelayCommand<ProjectNode?> NavigateToTreemapBreadcrumbCommand => _navigateToTreemapBreadcrumbCommand;

    internal ShareSnapshotViewModel? CreateShareSnapshotViewModel() =>
        _workspacePresenter.CreateShareSnapshotViewModel();

    public void DrillIntoTreemap(ProjectNode? node)
    {
        _workspacePresenter.DrillIntoTreemap(node);
    }

    public bool CanSetTreemapRoot(ProjectNode? node) => _workspacePresenter.CanSetTreemapRoot(node);

    public void SetTreemapRoot(ProjectNode? node) => _workspacePresenter.SetTreemapRoot(node);

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

    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    private void OpenShareSnapshot()
    {
        ShareSnapshot = CreateShareSnapshotFromCurrentState(ShareSnapshot);
    }

    private void CloseShareSnapshot()
    {
        ShareSnapshot = null;
    }

    public bool CanExcludeNodeFromFolder(ProjectNode? node) => ExcludesEditor.CanExcludeNodeFromFolder(node);

    private void WorkspacePresenterOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(MainWindowWorkspacePresenter.CanResetTreemapRoot))
        {
            _resetTreemapRootCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(MainWindowWorkspacePresenter.HasSnapshot))
        {
            _openShareSnapshotCommand.NotifyCanExecuteChanged();
            if (ShareSnapshot is not null)
            {
                ShareSnapshot = CreateShareSnapshotFromCurrentState(ShareSnapshot);
            }
        }
    }

    private ShareSnapshotViewModel? CreateShareSnapshotFromCurrentState(ShareSnapshotViewModel? previousState)
    {
        var currentState = CreateShareSnapshotViewModel();
        if (currentState is null)
        {
            return null;
        }

        if (previousState is null)
        {
            return currentState;
        }

        currentState.IncludeProjectName = previousState.IncludeProjectName;
        currentState.ProjectName = previousState.ProjectName;
        return currentState;
    }
}
