using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Models;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Logging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.Services;

public sealed partial class AnalysisSessionController : ObservableObject, IAnalysisSessionController
{
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAppLogger _logger;

    private CancellationTokenSource? _analysisCancellationTokenSource;
    private int _analysisVersion;

    public AnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IAppLogger? logger = null)
    {
        _projectAnalyzer = projectAnalyzer;
        _folderPickerService = folderPickerService;
        _logger = logger ?? NullAppLogger.Instance;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFolder))]
    private string? selectedFolderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSnapshot))]
    private ProjectSnapshot? currentSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private AnalysisState state = AnalysisState.Idle;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private AnalysisProgress? currentProgress;

    public bool HasSelectedFolder => !string.IsNullOrWhiteSpace(SelectedFolderPath);

    public bool HasSnapshot => CurrentSnapshot is not null;

    public bool IsBusy => State == AnalysisState.Scanning;

    public async Task OpenFolderAsync(ScanOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var selectedFolder = await _folderPickerService.PickFolderAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        SelectedFolderPath = selectedFolder;
        _logger.LogInformation($"Folder selected for analysis: '{selectedFolder}'.");

        await AnalyzeCurrentFolderAsync(options);
    }

    public Task RescanAsync(ScanOptions options) => AnalyzeCurrentFolderAsync(options);

    public void Cancel()
    {
        if (!HasSelectedFolder)
        {
            return;
        }

        _logger.LogInformation($"Cancellation requested for '{SelectedFolderPath}'.");
        _analysisCancellationTokenSource?.Cancel();
    }

    private async Task AnalyzeCurrentFolderAsync(ScanOptions options)
    {
        if (!HasSelectedFolder)
        {
            return;
        }

        var folderPath = SelectedFolderPath!;
        var version = Interlocked.Increment(ref _analysisVersion);
        var cancellationTokenSource = new CancellationTokenSource();

        var previousCancellationTokenSource = Interlocked.Exchange(
            ref _analysisCancellationTokenSource,
            cancellationTokenSource);
        previousCancellationTokenSource?.Cancel();
        previousCancellationTokenSource?.Dispose();

        CurrentProgress = null;
        SetState(AnalysisState.Scanning, $"Analyzing {folderPath}");

        var progress = new Progress<AnalysisProgress>(value =>
        {
            if (version == _analysisVersion && ReferenceEquals(_analysisCancellationTokenSource, cancellationTokenSource))
            {
                CurrentProgress = value;
            }
        });

        try
        {
            var snapshot = await _projectAnalyzer.AnalyzeAsync(
                folderPath,
                options,
                progress,
                cancellationTokenSource.Token);

            if (version != _analysisVersion)
            {
                return;
            }

            CurrentSnapshot = snapshot;
            CurrentProgress = null;
            SetState(AnalysisState.Completed);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            if (version != _analysisVersion)
            {
                return;
            }

            _logger.LogInformation($"Analysis cancelled from UI for '{folderPath}'.");
            CurrentProgress = null;
            SetState(AnalysisState.Cancelled, "Analysis cancelled.");
        }
        catch (Exception exception)
        {
            if (version != _analysisVersion)
            {
                return;
            }

            _logger.LogError(exception, $"Analysis failed in UI flow for '{folderPath}'.");
            CurrentProgress = null;
            SetState(AnalysisState.Failed, exception.Message);
        }
        finally
        {
            if (ReferenceEquals(_analysisCancellationTokenSource, cancellationTokenSource))
            {
                _analysisCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void SetState(AnalysisState value, string? message = null)
    {
        State = value;
        StatusMessage = message;
    }
}
