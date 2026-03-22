using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Models;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly RelayCommand _resetTreemapRootCommand;
    private readonly RelayCommand _toggleSettingsCommand;
    private CancellationTokenSource? _analysisCancellationTokenSource;
    private ProjectSnapshot? _currentSnapshot;
    private string? _selectedFolderPath;
    private ProjectNode? _treemapRootNode;

    public MainWindowViewModel()
        : this(new NullProjectAnalyzer(), new NullFolderPickerService())
    {
    }

    public MainWindowViewModel(IProjectAnalyzer projectAnalyzer, IFolderPickerService folderPickerService)
    {
        _projectAnalyzer = projectAnalyzer;
        _folderPickerService = folderPickerService;

        Toolbar = new ToolbarViewModel(
            new AsyncRelayCommand(OpenFolderAsync, CanOpenFolder),
            new AsyncRelayCommand(RescanAsync, CanRescan),
            new RelayCommand(CancelAnalysis, CanCancel));
        _resetTreemapRootCommand = new RelayCommand(ResetTreemapRoot, () => CanResetTreemapRoot);
        _toggleSettingsCommand = new RelayCommand(ToggleSettings);
        Tree = new ProjectTreeViewModel();
        Summary = new SummaryViewModel();

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
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
                _resetTreemapRootCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IRelayCommand ResetTreemapRootCommand => _resetTreemapRootCommand;

    public IRelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

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
            SetState(AnalysisState.Cancelled, "Analysis cancelled.");
        }
        catch (Exception exception)
        {
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

    private static bool CanDrillIntoTreemap(ProjectNode? node) =>
        node is not null &&
        node.Kind != Core.Enums.ProjectNodeKind.File &&
        node.Children.Count > 0;

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
}
