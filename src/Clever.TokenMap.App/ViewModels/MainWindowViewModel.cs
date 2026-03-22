using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Models;
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
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IAppLogger _logger;
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IThemeService _themeService;
    private readonly RelayCommand<ProjectNode?> _navigateToTreemapBreadcrumbCommand;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly RelayCommand _toggleSettingsCommand;
    private CancellationTokenSource? _analysisCancellationTokenSource;
    private AppSettings _currentSettings;
    private ProjectSnapshot? _currentSnapshot;
    private bool _isApplyingSettings;
    private string? _selectedFolderPath;
    private ProjectNode? _treemapRootNode;

    public MainWindowViewModel()
        : this(new NullProjectAnalyzer(), new NullFolderPickerService(), new NullAppSettingsStore(), new NullThemeService(), NullAppLogger.Instance)
    {
    }

    public MainWindowViewModel(IProjectAnalyzer projectAnalyzer, IFolderPickerService folderPickerService)
        : this(projectAnalyzer, folderPickerService, new NullAppSettingsStore(), new NullThemeService(), NullAppLogger.Instance)
    {
    }

    public MainWindowViewModel(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IAppSettingsStore appSettingsStore,
        IAppLogger? logger = null)
        : this(projectAnalyzer, folderPickerService, appSettingsStore, new NullThemeService(), logger)
    {
    }

    public MainWindowViewModel(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IAppSettingsStore appSettingsStore,
        IThemeService themeService,
        IAppLogger? logger = null)
    {
        _projectAnalyzer = projectAnalyzer;
        _folderPickerService = folderPickerService;
        _appSettingsStore = appSettingsStore;
        _themeService = themeService;
        _logger = logger ?? NullAppLogger.Instance;
        _currentSettings = AppSettings.CreateDefault();

        Toolbar = new ToolbarViewModel(
            new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder),
            new AsyncRelayCommand(RescanAsync, CanRescan),
            new RelayCommand(CancelAnalysis, CanCancel));
        _navigateToTreemapBreadcrumbCommand = new RelayCommand<ProjectNode?>(NavigateToTreemapBreadcrumb);
        _resetTreemapRootCommand = new RelayCommand(ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);
        Tree = new ProjectTreeViewModel();
        Summary = new SummaryViewModel();

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
        Toolbar.PropertyChanged += ToolbarOnPropertyChanged;
        LoadSettings();
        Toolbar.RefreshAvailability(hasSelectedFolder: false, isBusy: false, hasSnapshot: false);
    }

    public string WindowTitle => "TokenMap";

    public ToolbarViewModel Toolbar { get; }

    public ProjectTreeViewModel Tree { get; }

    public SummaryViewModel Summary { get; }

    public ProjectNode? TreemapRootNode
    {
        get => _treemapRootNode;
        private set
        {
            if (SetProperty(ref _treemapRootNode, value))
            {
                OnPropertyChanged(nameof(CanResetTreemapRoot));
                OnPropertyChanged(nameof(TreemapScopeDisplay));
                TreemapBreadcrumbs = BuildTreemapBreadcrumbs(value);
                _resetTreemapRootCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IRelayCommand ResetTreemapRootCommand => _resetTreemapRootCommand;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public IRelayCommand<ProjectNode?> NavigateToTreemapBreadcrumbCommand => _navigateToTreemapBreadcrumbCommand;

    public bool CanResetTreemapRoot =>
        _currentSnapshot is not null &&
        TreemapRootNode is not null &&
        !string.Equals(TreemapRootNode.Id, _currentSnapshot.Root.Id, StringComparison.Ordinal);

    public bool CanShowTreemapScope => CanResetTreemapRoot;

    public string TreemapScopeDisplay =>
        _currentSnapshot is null || TreemapRootNode is null
            ? string.Empty
            : CanResetTreemapRoot
                ? TreemapRootNode.RelativePath
                : string.Empty;

    [ObservableProperty]
    private IReadOnlyList<TreemapBreadcrumbItemViewModel> treemapBreadcrumbs = [];

    [ObservableProperty]
    private ProjectNode? selectedNode;

    [ObservableProperty]
    private AnalysisState analysisState = AnalysisState.Idle;

    [ObservableProperty]
    private bool isSettingsOpen;

    private bool CanOpenFolder() => AnalysisState != AnalysisState.Scanning;

    private bool CanRescan() =>
        AnalysisState != AnalysisState.Scanning &&
        !string.IsNullOrWhiteSpace(_selectedFolderPath);

    private bool CanCancel() => AnalysisState == AnalysisState.Scanning;

    private async Task OpenFolderAsync()
    {
        var selectedFolder = await _folderPickerService.PickFolderAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        _selectedFolderPath = selectedFolder;
        _logger.LogInformation($"Folder selected for analysis: '{selectedFolder}'.");
        Toolbar.UpdateFolder(selectedFolder);
        Toolbar.RefreshAvailability(hasSelectedFolder: true, isBusy: false, hasSnapshot: _currentSnapshot is not null);

        await AnalyzeCurrentFolderAsync();
    }

    private Task RescanAsync() => AnalyzeCurrentFolderAsync();

    private async Task AnalyzeCurrentFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedFolderPath))
        {
            return;
        }

        _analysisCancellationTokenSource?.Dispose();
        _analysisCancellationTokenSource = new CancellationTokenSource();

        SetState(AnalysisState.Scanning, $"Analyzing {_selectedFolderPath}");
        Toolbar.RefreshAvailability(hasSelectedFolder: true, isBusy: true, hasSnapshot: _currentSnapshot is not null);

        var progress = new Progress<AnalysisProgress>(value => Summary.UpdateProgress(value));

        try
        {
            var snapshot = await _projectAnalyzer.AnalyzeAsync(
                _selectedFolderPath,
                Toolbar.BuildScanOptions(),
                progress,
                _analysisCancellationTokenSource.Token);

            ApplySnapshot(snapshot);
            SetState(AnalysisState.Completed);
        }
        catch (OperationCanceledException) when (_analysisCancellationTokenSource.IsCancellationRequested)
        {
            _logger.LogInformation($"Analysis cancelled from UI for '{_selectedFolderPath}'.");
            SetState(AnalysisState.Cancelled, "Analysis cancelled.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Analysis failed in UI flow for '{_selectedFolderPath}'.");
            SetState(AnalysisState.Failed, exception.Message);
        }
        finally
        {
            _analysisCancellationTokenSource.Dispose();
            _analysisCancellationTokenSource = null;
            Toolbar.RefreshAvailability(
                hasSelectedFolder: !string.IsNullOrWhiteSpace(_selectedFolderPath),
                isBusy: false,
                hasSnapshot: _currentSnapshot is not null);
        }
    }

    private void ApplySnapshot(ProjectSnapshot snapshot)
    {
        _currentSnapshot = snapshot;
        var rootNode = new ProjectTreeNodeViewModel(snapshot.Root);
        TreemapRootNode = snapshot.Root;
        Tree.LoadRoot(rootNode);
        Summary.SetCompleted(snapshot);
        SelectedNode = snapshot.Root;
        Toolbar.RefreshAvailability(
            hasSelectedFolder: !string.IsNullOrWhiteSpace(_selectedFolderPath),
            isBusy: false,
            hasSnapshot: true);
    }

    public void DrillIntoTreemap(ProjectNode? node)
    {
        if (!CanDrillIntoTreemap(node))
        {
            return;
        }

        TreemapRootNode = node;
        SelectedNode = node;
    }

    private void CancelAnalysis()
    {
        _logger.LogInformation($"Cancellation requested for '{_selectedFolderPath}'.");
        _analysisCancellationTokenSource?.Cancel();
    }

    private void SetState(AnalysisState state, string? message = null)
    {
        AnalysisState = state;
        Summary.SetState(state, message);
    }

    partial void OnSelectedNodeChanged(ProjectNode? value)
    {
        if (value is not null)
        {
            Tree.SelectNodeById(value.Id);
        }
    }

    private void ResetTreemapRoot()
    {
        if (_currentSnapshot is null)
        {
            return;
        }

        TreemapRootNode = _currentSnapshot.Root;
    }

    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    private void NavigateToTreemapBreadcrumb(ProjectNode? node)
    {
        if (node is null)
        {
            return;
        }

        TreemapRootNode = node;
    }

    private IReadOnlyList<TreemapBreadcrumbItemViewModel> BuildTreemapBreadcrumbs(ProjectNode? node)
    {
        if (_currentSnapshot is null || node is null)
        {
            return [];
        }

        var path = new List<ProjectNode>();
        if (!TryBuildNodePath(_currentSnapshot.Root, node.Id, path))
        {
            return [];
        }

        var items = new List<TreemapBreadcrumbItemViewModel>(path.Count);
        for (var index = 0; index < path.Count; index++)
        {
            var pathNode = path[index];
            var label = index == 0
                ? pathNode.Name
                : $"/ {pathNode.Name}";
            items.Add(new TreemapBreadcrumbItemViewModel(
                label,
                pathNode,
                canNavigate: index < path.Count - 1));
        }

        return items;
    }

    private static bool TryBuildNodePath(ProjectNode current, string targetId, List<ProjectNode> path)
    {
        path.Add(current);
        if (string.Equals(current.Id, targetId, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (TryBuildNodePath(child, targetId, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static bool CanDrillIntoTreemap(ProjectNode? node) =>
        node is not null &&
        node.Kind != Core.Enums.ProjectNodeKind.File &&
        node.Children.Count > 0;

    private void LoadSettings()
    {
        _currentSettings = _appSettingsStore.Load();
        _isApplyingSettings = true;

        try
        {
            Toolbar.ApplyAnalysisSettings(_currentSettings.Analysis);
            Toolbar.ApplyAppearanceSettings(_currentSettings.Appearance);
            _themeService.ApplyThemePreference(_currentSettings.Appearance.ThemePreference);
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void ToolbarOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        if (e.PropertyName is nameof(ToolbarViewModel.SelectedMetric) or
            nameof(ToolbarViewModel.SelectedTokenProfile) or
            nameof(ToolbarViewModel.RespectGitIgnore) or
            nameof(ToolbarViewModel.RespectIgnore) or
            nameof(ToolbarViewModel.UseDefaultExcludes) or
            nameof(ToolbarViewModel.SelectedThemePreference))
        {
            _currentSettings.Analysis = Toolbar.BuildAnalysisSettings();
            _currentSettings.Appearance = Toolbar.BuildAppearanceSettings();
            _themeService.ApplyThemePreference(_currentSettings.Appearance.ThemePreference);
            _appSettingsStore.Save(_currentSettings);
            _logger.LogDebug("Persisted updated analysis settings to settings.json.");
        }
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

    private sealed class NullAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Load() => AppSettings.CreateDefault();

        public void Save(AppSettings settings)
        {
        }
    }

    private sealed class NullThemeService : IThemeService
    {
        public string CurrentSystemTheme => ThemePreferences.Light;

        public void ApplyThemePreference(string themePreference)
        {
        }
    }
}
