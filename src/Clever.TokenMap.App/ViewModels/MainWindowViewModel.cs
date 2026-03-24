using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string GlobalExcludesHelperText = "Use gitignore-style rules, one per line. Use / for project-root rules, ! for re-include rules, and # for comments.";
    private const string FolderExcludesHelperText = "Use gitignore-style rules, one per line. These rules apply only to the current folder and override .gitignore. Use / for folder-root rules, ! for re-include rules, and # for comments.";

    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly IFolderPathService _folderPathService;
    private readonly IPathShellService _pathShellService;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly TreemapNavigationState _treemapNavigationState;
    private readonly ObservableCollection<RecentFolderItemViewModel> _recentFolders = [];
    private readonly ObservableCollection<RecentFolderItemViewModel> _recentFolderFlyoutItems = [];
    private readonly RelayCommand _cancelExcludesEditorCommand;
    private readonly RelayCommand _clearRecentFoldersCommand;
    private readonly RelayCommand _closeSettingsCommand;
    private readonly RelayCommand<ProjectNode?> _excludeNodeFromFolderCommand;
    private readonly RelayCommand<ProjectNode?> _navigateToTreemapBreadcrumbCommand;
    private readonly RelayCommand _openFolderExcludesEditorCommand;
    private readonly RelayCommand _openGlobalExcludesEditorCommand;
    private readonly AsyncRelayCommand<RecentFolderItemViewModel?> _openRecentFolderCommand;
    private readonly RelayCommand<RecentFolderItemViewModel?> _removeRecentFolderCommand;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly AsyncRelayCommand _saveAndRescanExcludesEditorCommand;
    private readonly RelayCommand _saveExcludesEditorCommand;
    private readonly RelayCommand _toggleSettingsCommand;
    private ExcludesEditorScope _activeExcludesEditorScope;
    private bool _isLoadingExcludesEditorText;

    private enum ExcludesEditorScope
    {
        Global,
        Folder,
    }

    [ObservableProperty]
    private bool isSettingsOpen;

    [ObservableProperty]
    private bool isExcludesEditorOpen;

    [ObservableProperty]
    private string excludesEditorText = string.Join(Environment.NewLine, GlobalExcludeDefaults.DefaultEntries);

    [ObservableProperty]
    private string excludesEditorTitle = "Global excludes";

    [ObservableProperty]
    private string excludesEditorHelperText = GlobalExcludesHelperText;

    [ObservableProperty]
    private bool showScanSettingsRescanNotice;

    public MainWindowViewModel()
        : this(
            CreateAnalysisSessionController(
                new NullProjectAnalyzer(),
                new NullFolderPickerService(),
                new NullFolderPathService(),
                NullAppLogger.Instance),
            new TreemapNavigationState(),
            new NullSettingsCoordinator(),
            new NullFolderPathService(),
            new NullPathShellService())
    {
    }

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
        _folderPathService = folderPathService;
        _pathShellService = pathShellService;

        Toolbar = new ToolbarViewModel(
            _settingsCoordinator.State,
            _settingsCoordinator.CurrentFolderState,
            new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder),
            new AsyncRelayCommand(RescanAsync, CanRescan),
            new RelayCommand(CancelAnalysis, CanCancel));
        RecentFolders = new ReadOnlyObservableCollection<RecentFolderItemViewModel>(_recentFolders);
        RecentFolderFlyoutItems = new ReadOnlyObservableCollection<RecentFolderItemViewModel>(_recentFolderFlyoutItems);
        Tree = new ProjectTreeViewModel();
        Summary = new SummaryViewModel();

        _excludeNodeFromFolderCommand = new RelayCommand<ProjectNode?>(ExcludeNodeFromFolder, CanExcludeNodeFromFolder);
        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(NavigateToTreemapBreadcrumb);
        _cancelExcludesEditorCommand = new RelayCommand(CancelExcludesEditor);
        _clearRecentFoldersCommand = new RelayCommand(ClearRecentFolders);
        _closeSettingsCommand = new RelayCommand(CloseSettings);
        _openFolderExcludesEditorCommand = new RelayCommand(OpenFolderExcludesEditor, () => Toolbar.HasCurrentFolderSettings);
        _openRecentFolderCommand = new AsyncRelayCommand<RecentFolderItemViewModel?>(OpenRecentFolderAsync);
        _openGlobalExcludesEditorCommand = new RelayCommand(OpenGlobalExcludesEditor);
        _removeRecentFolderCommand = new RelayCommand<RecentFolderItemViewModel?>(RemoveRecentFolder);
        _resetTreemapRootCommand = new RelayCommand(ResetTreemapRoot, () => CanResetTreemapRoot);
        _saveAndRescanExcludesEditorCommand = new AsyncRelayCommand(SaveAndRescanExcludesEditorAsync, CanSaveAndRescanExcludesEditor);
        _saveExcludesEditorCommand = new RelayCommand(SaveExcludesEditor);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _settingsCoordinator.State.RecentFolderPathsChanged += RecentFolderPathsOnCollectionChanged;
        _treemapNavigationState.PropertyChanged += TreemapNavigationStateOnPropertyChanged;
        RefreshRecentFolders();

        _settingsCoordinator.SwitchActiveFolder(_analysisSessionController.SelectedFolderPath);
        _openFolderExcludesEditorCommand.NotifyCanExecuteChanged();
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

    public string WindowTitle => BuildWindowTitle(_analysisSessionController.SelectedFolderPath);

    public ToolbarViewModel Toolbar { get; }

    public ProjectTreeViewModel Tree { get; }

    public SummaryViewModel Summary { get; }

    public ReadOnlyObservableCollection<RecentFolderItemViewModel> RecentFolders { get; }

    public ReadOnlyObservableCollection<RecentFolderItemViewModel> RecentFolderFlyoutItems { get; }

    public ProjectNode? TreemapRootNode => _treemapNavigationState.TreemapRootNode;

    public bool HasRecentFolders => RecentFolders.Count > 0;

    public bool HasSnapshot => _analysisSessionController.HasSnapshot;

    public bool ShowRecentStartSurface => !HasSnapshot;

    public bool ShowRecentFoldersEmptyState => !HasRecentFolders;

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

    public IRelayCommand CloseSettingsCommand => _closeSettingsCommand;

    public IRelayCommand<ProjectNode?> ExcludeNodeFromFolderCommand => _excludeNodeFromFolderCommand;

    public IRelayCommand OpenGlobalExcludesEditorCommand => _openGlobalExcludesEditorCommand;

    public IRelayCommand OpenFolderExcludesEditorCommand => _openFolderExcludesEditorCommand;

    public IRelayCommand CancelExcludesEditorCommand => _cancelExcludesEditorCommand;

    public IRelayCommand SaveExcludesEditorCommand => _saveExcludesEditorCommand;

    public IAsyncRelayCommand SaveAndRescanExcludesEditorCommand => _saveAndRescanExcludesEditorCommand;

    public IRelayCommand ClearRecentFoldersCommand => _clearRecentFoldersCommand;

    public IRelayCommand<ProjectNode?> NavigateToTreemapBreadcrumbCommand => _navigateToTreemapBreadcrumbCommand;

    public IAsyncRelayCommand<RecentFolderItemViewModel?> OpenRecentFolderCommand => _openRecentFolderCommand;

    public IRelayCommand<RecentFolderItemViewModel?> RemoveRecentFolderCommand => _removeRecentFolderCommand;

    private bool CanOpenFolder() => !_analysisSessionController.IsBusy;

    private bool CanRescan() => !_analysisSessionController.IsBusy && _analysisSessionController.HasSelectedFolder;

    private bool CanCancel() => _analysisSessionController.IsBusy;

    private bool CanSaveAndRescanExcludesEditor() => !_analysisSessionController.IsBusy && _analysisSessionController.HasSelectedFolder;

    public void DrillIntoTreemap(ProjectNode? node)
    {
        _treemapNavigationState.DrillInto(node);
    }

    public bool CanSetTreemapRoot(ProjectNode? node) => _treemapNavigationState.CanSetTreemapRoot(node);

    public bool SetTreemapRoot(ProjectNode? node) => _treemapNavigationState.SetTreemapRoot(node);

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

    private async Task OpenRecentFolderAsync(RecentFolderItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        if (!folder.CanOpen)
        {
            return;
        }

        await _analysisSessionController.OpenFolderAsync(folder.FullPath, Toolbar.BuildScanOptions());
    }

    private void RemoveRecentFolder(RecentFolderItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        _settingsCoordinator.State.RemoveRecentFolder(folder.FullPath);
    }

    private void ClearRecentFolders()
    {
        _settingsCoordinator.State.ClearRecentFolders();
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

    private void OpenGlobalExcludesEditor()
    {
        _activeExcludesEditorScope = ExcludesEditorScope.Global;
        ExcludesEditorTitle = "Global excludes";
        ExcludesEditorHelperText = GlobalExcludesHelperText;
        LoadExcludesEditorText(_settingsCoordinator.State.GlobalExcludes);
        DismissScanSettingsRescanNotice();
        IsExcludesEditorOpen = true;
        RefreshEditorCommandAvailability();
    }

    private void OpenFolderExcludesEditor()
    {
        OpenFolderExcludesEditorCore(entryToAppend: null);
    }

    private void OpenFolderExcludesEditorCore(string? entryToAppend)
    {
        if (!_settingsCoordinator.CurrentFolderState.HasActiveFolder)
        {
            return;
        }

        _activeExcludesEditorScope = ExcludesEditorScope.Folder;
        ExcludesEditorTitle = $"Excludes for {GetFolderDisplayName(_settingsCoordinator.CurrentFolderState.ActiveRootPath)}";
        ExcludesEditorHelperText = FolderExcludesHelperText;
        var entries = _settingsCoordinator.CurrentFolderState.FolderExcludes.ToList();
        if (!string.IsNullOrWhiteSpace(entryToAppend) &&
            !entries.Contains(entryToAppend, StringComparer.Ordinal))
        {
            entries.Add(entryToAppend);
        }

        LoadExcludesEditorText(entries);
        DismissScanSettingsRescanNotice();
        IsExcludesEditorOpen = true;
        RefreshEditorCommandAvailability();
    }

    private void CancelExcludesEditor()
    {
        IsExcludesEditorOpen = false;
        RefreshEditorCommandAvailability();
    }

    private void SaveExcludesEditor()
    {
        var changed = SaveExcludesEditorInternal();
        ShowScanSettingsRescanNotice = changed && _analysisSessionController.HasSelectedFolder;
    }

    private async Task SaveAndRescanExcludesEditorAsync()
    {
        SaveExcludesEditorInternal();

        if (_analysisSessionController.HasSelectedFolder && !_analysisSessionController.IsBusy)
        {
            await _analysisSessionController.RescanAsync(Toolbar.BuildScanOptions());
        }
    }

    private bool SaveExcludesEditorInternal()
    {
        var updatedEntries = ParseExcludeEditorText(ExcludesEditorText);
        var changed = SaveExcludesByScope(updatedEntries);

        IsExcludesEditorOpen = false;
        RefreshEditorCommandAvailability();
        return changed;
    }

    private void NavigateToTreemapBreadcrumb(ProjectNode? node)
    {
        _treemapNavigationState.NavigateToBreadcrumb(node);
    }

    public bool CanExcludeNodeFromFolder(ProjectNode? node) =>
        !_analysisSessionController.IsBusy &&
        _settingsCoordinator.CurrentFolderState.HasActiveFolder &&
        node is not null &&
        !string.IsNullOrWhiteSpace(node.RelativePath) &&
        node.Kind is not Core.Enums.ProjectNodeKind.Root;

    private void ExcludeNodeFromFolder(ProjectNode? node)
    {
        if (!CanExcludeNodeFromFolder(node))
        {
            return;
        }

        OpenFolderExcludesEditorCore(BuildFolderExcludeEntry(node!));
    }

    private void AnalysisSessionControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnalysisSessionController.SelectedFolderPath):
                _settingsCoordinator.SwitchActiveFolder(_analysisSessionController.SelectedFolderPath);
                Toolbar.UpdateFolder(_analysisSessionController.SelectedFolderPath);
                OnPropertyChanged(nameof(WindowTitle));
                _openFolderExcludesEditorCommand.NotifyCanExecuteChanged();
                _excludeNodeFromFolderCommand.NotifyCanExecuteChanged();
                if (_activeExcludesEditorScope == ExcludesEditorScope.Folder && IsExcludesEditorOpen)
                {
                    IsExcludesEditorOpen = false;
                    RefreshEditorCommandAvailability();
                }

                DismissScanSettingsRescanNotice();
                RefreshToolbarAvailability();
                break;
            case nameof(IAnalysisSessionController.CurrentSnapshot):
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(HasSnapshot));
                OnPropertyChanged(nameof(ShowRecentStartSurface));
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
                if (_analysisSessionController.State == AnalysisState.Scanning)
                {
                    DismissScanSettingsRescanNotice();
                }
                RefreshEditorCommandAvailability();

                if (_analysisSessionController.State == AnalysisState.Completed &&
                    _analysisSessionController.CurrentSnapshot is { } completedSnapshot)
                {
                    Summary.SetCompleted(completedSnapshot);
                    _settingsCoordinator.State.RecordRecentFolder(completedSnapshot.RootPath);
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

    private void RecentFolderPathsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentFolders();
        OnPropertyChanged(nameof(HasRecentFolders));
        OnPropertyChanged(nameof(ShowRecentStartSurface));
        OnPropertyChanged(nameof(ShowRecentFoldersEmptyState));
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
        RefreshEditorCommandAvailability();
    }

    partial void OnExcludesEditorTextChanged(string value)
    {
        if (!_isLoadingExcludesEditorText)
        {
            DismissScanSettingsRescanNotice();
        }
    }

    private void LoadExcludesEditorText(IEnumerable<string> entries)
    {
        _isLoadingExcludesEditorText = true;
        try
        {
            ExcludesEditorText = string.Join(Environment.NewLine, entries);
        }
        finally
        {
            _isLoadingExcludesEditorText = false;
        }
    }

    private void DismissScanSettingsRescanNotice()
    {
        ShowScanSettingsRescanNotice = false;
    }

    private static IReadOnlyList<string> ParseExcludeEditorText(string? text) =>
        GlobalExcludeList.Normalize((text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'));

    private bool SaveExcludesByScope(IReadOnlyList<string> updatedEntries)
    {
        return _activeExcludesEditorScope switch
        {
            ExcludesEditorScope.Folder => SaveFolderExcludes(updatedEntries),
            _ => SaveGlobalExcludes(updatedEntries),
        };
    }

    private bool SaveGlobalExcludes(IReadOnlyList<string> updatedEntries)
    {
        var changed = !_settingsCoordinator.State.GlobalExcludes.SequenceEqual(updatedEntries, StringComparer.Ordinal);
        _settingsCoordinator.State.ReplaceGlobalExcludes(updatedEntries);
        return changed;
    }

    private bool SaveFolderExcludes(IReadOnlyList<string> updatedEntries)
    {
        if (!_settingsCoordinator.CurrentFolderState.HasActiveFolder)
        {
            return false;
        }

        var changed = !_settingsCoordinator.CurrentFolderState.FolderExcludes.SequenceEqual(updatedEntries, StringComparer.Ordinal);
        if (!_settingsCoordinator.CurrentFolderState.UseFolderExcludes)
        {
            _settingsCoordinator.CurrentFolderState.UseFolderExcludes = true;
            changed = true;
        }

        _settingsCoordinator.CurrentFolderState.ReplaceFolderExcludes(updatedEntries);
        return changed;
    }

    private void RefreshEditorCommandAvailability()
    {
        _saveAndRescanExcludesEditorCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRecentFolders()
    {
        _recentFolders.Clear();
        _recentFolderFlyoutItems.Clear();

        foreach (var folderPath in _settingsCoordinator.State.RecentFolderPaths)
        {
            var item = CreateRecentFolderItem(folderPath);
            _recentFolders.Add(item);
            _recentFolderFlyoutItems.Add(item);
        }

        if (_recentFolderFlyoutItems.Count == 0)
        {
            _recentFolderFlyoutItems.Add(CreateEmptyFlyoutItem());
        }
    }

    private RecentFolderItemViewModel CreateRecentFolderItem(string folderPath)
    {
        return new RecentFolderItemViewModel(
            GetFolderDisplayName(folderPath),
            folderPath.Trim(),
            isMissing: !_folderPathService.Exists(folderPath.Trim()));
    }

    private static RecentFolderItemViewModel CreateEmptyFlyoutItem()
    {
        return new RecentFolderItemViewModel(
            "No previous folders yet",
            string.Empty,
            secondaryText: "Analyze a folder once and it will appear here.",
            canOpen: false,
            showFolderIcon: false);
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

    private static string BuildFolderExcludeEntry(ProjectNode node)
    {
        var relativePath = node.RelativePath.Replace('\\', '/').Trim('/');
        return node.Kind is Core.Enums.ProjectNodeKind.Directory
            ? $"/{relativePath}/"
            : $"/{relativePath}";
    }

    private static AnalysisSessionController CreateAnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService,
        IAppLogger? logger)
    {
        ArgumentNullException.ThrowIfNull(projectAnalyzer);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(folderPathService);

        return new AnalysisSessionController(projectAnalyzer, folderPickerService, folderPathService, logger, scanOptionsResolver: null);
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

    private sealed class NullFolderPathService : IFolderPathService
    {
        public bool Exists(string folderPath) => true;
    }

    private sealed class NullSettingsCoordinator : ISettingsCoordinator
    {
        public SettingsState State { get; } = new();

        public CurrentFolderSettingsState CurrentFolderState { get; } = new();

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) => baseOptions;

        public void SwitchActiveFolder(string? rootPath)
        {
        }
    }

    private sealed class NullPathShellService : IPathShellService
    {
        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
