using System;
using System.ComponentModel;
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
    private CancellationTokenSource? _analysisCancellationTokenSource;
    private ProjectSnapshot? _currentSnapshot;
    private string? _selectedFolderPath;

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
        Tree = new ProjectTreeViewModel();
        Details = new DetailsPanelViewModel();
        Summary = new SummaryViewModel();

        Tree.SelectedNodeChanged += (_, node) => SelectedNode = node?.Node;
        Toolbar.PropertyChanged += HandleToolbarPropertyChanged;
        Toolbar.RefreshAvailability(hasSelectedFolder: false, isBusy: false);
    }

    public string WindowTitle => "TokenMap";

    public ToolbarViewModel Toolbar { get; }

    public ProjectTreeViewModel Tree { get; }

    public DetailsPanelViewModel Details { get; }

    public SummaryViewModel Summary { get; }

    public ProjectNode? TreemapRootNode { get; private set; }

    [ObservableProperty]
    private ProjectNode? selectedNode;

    [ObservableProperty]
    private AnalysisState analysisState = AnalysisState.Idle;

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
        Toolbar.RefreshAvailability(hasSelectedFolder: true, isBusy: false);

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
        Toolbar.RefreshAvailability(hasSelectedFolder: true, isBusy: true);

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
            Toolbar.RefreshAvailability(hasSelectedFolder: !string.IsNullOrWhiteSpace(_selectedFolderPath), isBusy: false);
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
        OnPropertyChanged(nameof(TreemapRootNode));
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

        Details.ShowNode(value, _currentSnapshot?.Root, Toolbar.SelectedMetric);
    }

    private void HandleToolbarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolbarViewModel.SelectedMetric))
        {
            Details.ShowNode(SelectedNode, _currentSnapshot?.Root, Toolbar.SelectedMetric);
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
}
