using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPathShellService _pathShellService;
    private readonly IFilePreviewController _filePreviewController;
    private readonly IAppIssueReporter _issueReporter;
    private readonly MainWindowWorkspacePresenter _workspacePresenter;
    private readonly RelayCommand _closeFilePreviewCommand;
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
        AboutViewModel about,
        ToolbarViewModel toolbar,
        ExcludesEditorViewModel excludesEditor,
        FilePreviewState filePreview,
        RecentFoldersViewModel recentFolders,
        AppIssueViewModel issue,
        ProjectTreeViewModel tree,
        SummaryViewModel summary,
        IPathShellService pathShellService,
        IFilePreviewController filePreviewController,
        IAppIssueReporter issueReporter)
    {
        _workspacePresenter = workspacePresenter;
        _pathShellService = pathShellService;
        _filePreviewController = filePreviewController;
        _issueReporter = issueReporter;

        About = about;
        Toolbar = toolbar;
        ExcludesEditor = excludesEditor;
        FilePreview = filePreview;
        RecentFolders = recentFolders;
        Issue = issue;
        Tree = tree;
        Summary = summary;

        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(_workspacePresenter.NavigateToTreemapBreadcrumb);
        _closeFilePreviewCommand = new RelayCommand(CloseFilePreview, () => IsFilePreviewOpen);
        _closeSettingsCommand = new RelayCommand(CloseSettings);
        _closeShareSnapshotCommand = new RelayCommand(CloseShareSnapshot);
        _openShareSnapshotCommand = new RelayCommand(OpenShareSnapshot, () => HasSnapshot);
        _resetTreemapRootCommand = new RelayCommand(_workspacePresenter.ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);

        _workspacePresenter.PropertyChanged += WorkspacePresenterOnPropertyChanged;
        FilePreview.PropertyChanged += FilePreviewOnPropertyChanged;
    }

    public string WindowTitle => _workspacePresenter.WindowTitle;

    public string ProjectTreeSelectedFolderText => _workspacePresenter.ProjectTreeSelectedFolderText;

    public AboutViewModel About { get; }

    public ToolbarViewModel Toolbar { get; }

    public ExcludesEditorViewModel ExcludesEditor { get; }

    public FilePreviewState FilePreview { get; }

    public RecentFoldersViewModel RecentFolders { get; }

    public AppIssueViewModel Issue { get; }

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

    public bool IsFilePreviewOpen => FilePreview.IsOpen;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public IRelayCommand CloseSettingsCommand => _closeSettingsCommand;

    public IRelayCommand CloseFilePreviewCommand => _closeFilePreviewCommand;

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
        => OpenPathActionAsync(
            node,
            static (service, currentNode, token) => service.TryOpenAsync(currentNode.FullPath, token),
            code: "shell.open_node_failed",
            messageFactory: currentNode => $"TokenMap could not open '{currentNode.Name}'.",
            technicalMessage: "Opening a path through the shell failed.",
            cancellationToken: cancellationToken);

    public Task RevealNodeAsync(ProjectNode? node, CancellationToken cancellationToken = default)
        => OpenPathActionAsync(
            node,
            (service, currentNode, token) => service.TryRevealAsync(
                currentNode.FullPath,
                currentNode.Kind is not ProjectNodeKind.File,
                token),
            code: "shell.reveal_node_failed",
            messageFactory: currentNode => $"TokenMap could not reveal '{currentNode.Name}'.",
            technicalMessage: "Revealing a path through the shell failed.",
            cancellationToken: cancellationToken);

    public Task PreviewNodeAsync(ProjectNode? node, CancellationToken cancellationToken = default) =>
        _filePreviewController.OpenAsync(node, cancellationToken);

    public void CloseFilePreview()
    {
        _filePreviewController.Close();
    }

    private async Task OpenPathActionAsync(
        ProjectNode? node,
        Func<IPathShellService, ProjectNode, CancellationToken, Task<bool>> action,
        string code,
        Func<ProjectNode, string> messageFactory,
        string technicalMessage,
        CancellationToken cancellationToken = default)
    {
        if (node is null)
        {
            return;
        }

        var succeeded = await action(_pathShellService, node, cancellationToken).ConfigureAwait(false);
        if (succeeded)
        {
            return;
        }

        _issueReporter.Report(new AppIssue
        {
            Code = code,
            UserMessage = messageFactory(node),
            TechnicalMessage = technicalMessage,
            Context = AppIssueContext.Create(
                ("NodeName", node.Name),
                ("NodePath", node.FullPath),
                ("NodeKind", node.Kind)),
        });
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

    internal void ReportIssue(AppIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _issueReporter.Report(issue);
    }

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

    private void FilePreviewOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilePreviewState.IsOpen))
        {
            return;
        }

        OnPropertyChanged(nameof(IsFilePreviewOpen));
        _closeFilePreviewCommand.NotifyCanExecuteChanged();
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
