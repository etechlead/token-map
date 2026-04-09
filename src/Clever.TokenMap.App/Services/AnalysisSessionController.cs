using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.Services;

public sealed partial class AnalysisSessionController : ObservableObject, IAnalysisSessionController
{
    private readonly IProjectAnalyzer _projectAnalyzer;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFolderPathService _folderPathService;
    private readonly IScanOptionsResolver _scanOptionsResolver;
    private readonly IAppIssueReporter _issueReporter;
    private readonly IAppLogger _logger;
    private readonly LocalizationState? _localization;

    private CancellationTokenSource? _analysisCancellationTokenSource;
    private string? _activeAnalysisFolderPath;
    private int _analysisVersion;

    public AnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService,
        IAppLogger? logger = null,
        IScanOptionsResolver? scanOptionsResolver = null,
        IAppIssueReporter? issueReporter = null,
        LocalizationState? localization = null)
    {
        _projectAnalyzer = projectAnalyzer;
        _folderPickerService = folderPickerService;
        _folderPathService = folderPathService;
        _scanOptionsResolver = scanOptionsResolver ?? new PassthroughScanOptionsResolver();
        _logger = logger ?? NullAppLogger.Instance;
        _issueReporter = issueReporter ?? NullAppIssueReporter.Instance;
        _localization = localization;
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

        _logger.LogInformation(
            "Folder selected for analysis.",
            eventCode: "analysis.folder_selected",
            context: AppIssueContext.Create(("FolderPath", folderPath)));
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
        _logger.LogInformation(
            "Cancellation requested for the active analysis.",
            eventCode: "analysis.cancel_requested",
            context: AppIssueContext.Create(("FolderPath", activeFolderPath)));
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
            _issueReporter.Report(new AppIssue
            {
                Code = "analysis.root_missing",
                UserMessage = _localization?.FormatAnalysisRootMissing(folderPath) ?? $"TokenMap could not find '{folderPath}'.",
                TechnicalMessage = "The selected project root does not exist.",
                Context = AppIssueContext.Create(("FolderPath", folderPath)),
            });
            SetState(AnalysisState.Failed);
            return;
        }

        SetState(AnalysisState.Scanning);

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

            _logger.LogInformation(
                "Analysis cancelled from the UI flow.",
                eventCode: "analysis.cancelled",
                context: AppIssueContext.Create(("FolderPath", folderPath)));
            CurrentProgress = null;
            SetState(AnalysisState.Cancelled);
        }
        catch (Exception exception)
        {
            if (version != _analysisVersion)
            {
                return;
            }

            _issueReporter.Report(new AppIssue
            {
                Code = "analysis.run_failed",
                UserMessage = _localization?.FormatAnalysisRunFailed(folderPath) ?? $"TokenMap could not finish analyzing '{folderPath}'.",
                TechnicalMessage = "The analysis flow failed before the workspace state could be updated.",
                Exception = exception,
                Context = AppIssueContext.Create(("FolderPath", folderPath)),
            });
            CurrentProgress = null;
            SetState(AnalysisState.Failed);
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

    private void SetState(AnalysisState value)
    {
        State = value;
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
