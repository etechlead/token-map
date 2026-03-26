using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.App.State;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.Services;

public sealed partial class AnalysisSessionController : ObservableObject, IAnalysisSessionController
{
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFolderPathService _folderPathService;
    private readonly IScanOptionsResolver _scanOptionsResolver;
    private readonly IAppLogger _logger;

    private CancellationTokenSource? _analysisCancellationTokenSource;
    private string? _activeAnalysisFolderPath;
    private int _analysisVersion;

    public AnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService,
        IAppLogger? logger = null,
        IScanOptionsResolver? scanOptionsResolver = null)
    {
        _projectAnalyzer = projectAnalyzer;
        _folderPickerService = folderPickerService;
        _folderPathService = folderPathService;
        _scanOptionsResolver = scanOptionsResolver ?? new PassthroughScanOptionsResolver();
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

        await OpenFolderAsync(selectedFolder, options);
    }

    public Task OpenFolderAsync(string folderPath, ScanOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation($"Folder selected for analysis: '{folderPath}'.");
        return AnalyzeFolderAsync(folderPath, options, commitSelectedFolderOnSuccess: true);
    }

    public Task RescanAsync(ScanOptions options)
    {
        if (!HasSelectedFolder)
        {
            return Task.CompletedTask;
        }

        return AnalyzeFolderAsync(SelectedFolderPath!, options, commitSelectedFolderOnSuccess: false);
    }

    public void Cancel()
    {
        if (!IsBusy)
        {
            return;
        }

        var activeFolderPath = _activeAnalysisFolderPath ?? SelectedFolderPath ?? "<unknown>";
        _logger.LogInformation($"Cancellation requested for '{activeFolderPath}'.");
        _analysisCancellationTokenSource?.Cancel();
    }

    private async Task AnalyzeFolderAsync(
        string folderPath,
        ScanOptions options,
        bool commitSelectedFolderOnSuccess)
    {
        var resolvedOptions = _scanOptionsResolver.Resolve(folderPath, options);
        var version = Interlocked.Increment(ref _analysisVersion);
        var cancellationTokenSource = new CancellationTokenSource();

        var previousCancellationTokenSource = Interlocked.Exchange(
            ref _analysisCancellationTokenSource,
            cancellationTokenSource);
        previousCancellationTokenSource?.Cancel();
        previousCancellationTokenSource?.Dispose();

        _activeAnalysisFolderPath = folderPath;
        CurrentProgress = null;

        if (!_folderPathService.Exists(folderPath))
        {
            _logger.LogInformation($"Analysis skipped because project root was not found: '{folderPath}'.");
            SetState(AnalysisState.Failed, $"Project root was not found: {folderPath}");
            return;
        }

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
            var snapshot = await RunProjectAnalysisAsync(
                folderPath,
                resolvedOptions,
                progress,
                cancellationTokenSource.Token);

            if (version != _analysisVersion)
            {
                return;
            }

            if (commitSelectedFolderOnSuccess)
            {
                SelectedFolderPath = snapshot.RootPath;
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
                _activeAnalysisFolderPath = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void SetState(AnalysisState value, string? message = null)
    {
        State = value;
        StatusMessage = message;
    }

    private Task<ProjectSnapshot> RunProjectAnalysisAsync(
        string folderPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Keep the scanner and metrics pipeline off the caller thread so the UI stays responsive.
        return Task.Run(
            () => _projectAnalyzer.AnalyzeAsync(folderPath, options, progress, cancellationToken),
            CancellationToken.None);
    }

    private sealed class PassthroughScanOptionsResolver : IScanOptionsResolver
    {
        public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) => baseOptions;
    }
}
