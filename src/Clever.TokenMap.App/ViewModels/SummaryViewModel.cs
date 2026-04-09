using System;
using System.Globalization;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class SummaryViewModel : ViewModelBase, ISummaryProjection
{
    private readonly LocalizationState _localization;
    private bool _acceptProgressUpdates;
    private AnalysisState _state = AnalysisState.Idle;
    private ProjectSnapshot? _snapshot;
    private AnalysisProgress? _progress;

    public SummaryViewModel(LocalizationState localization)
    {
        _localization = localization;
        _localization.LanguageChanged += (_, _) => RefreshLocalizedState();
        summaryText = _localization.SummaryIdle;
        totalsText = _localization.FormatTotalsText(0, 0, 0, 0);
    }

    [ObservableProperty]
    private string summaryText;

    [ObservableProperty]
    private string totalsText;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private bool isProgressVisible;

    [ObservableProperty]
    private bool isProgressPillVisible;

    [ObservableProperty]
    private string progressPillText = string.Empty;

    [ObservableProperty]
    private string tokenSummaryValue = "0";

    [ObservableProperty]
    private string lineSummaryValue = "0";

    [ObservableProperty]
    private string fileSummaryValue = "0";

    [ObservableProperty]
    private string warningSummaryValue = "0";

    public void SetState(AnalysisState state)
    {
        _state = state;
        SummaryText = state switch
        {
            AnalysisState.Idle => _localization.SummaryIdle,
            AnalysisState.Scanning => _localization.SummaryScanning,
            AnalysisState.Cancelled => _localization.SummaryCancelled,
            AnalysisState.Failed => _localization.SummaryFailed,
            _ => SummaryText,
        };

        switch (state)
        {
            case AnalysisState.Scanning:
                _acceptProgressUpdates = true;
                ProgressValue = 0;
                IsProgressIndeterminate = true;
                IsProgressVisible = true;
                IsProgressPillVisible = true;
                ProgressPillText = _localization.ProgressScanningTree;
                break;
            default:
                _acceptProgressUpdates = false;
                ProgressValue = 0;
                IsProgressIndeterminate = false;
                IsProgressVisible = false;
                IsProgressPillVisible = false;
                ProgressPillText = string.Empty;
                break;
        }
    }

    public void SetCompleted(ProjectSnapshot snapshot)
    {
        _snapshot = snapshot;
        var tokenCount = snapshot.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens) ?? 0;
        var nonEmptyLineCount = snapshot.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.NonEmptyLines) ?? 0;
        var fileCount = snapshot.Root.Summary.DescendantFileCount;
        _acceptProgressUpdates = false;
        SummaryText = snapshot.Diagnostics.Count == 0
            ? _localization.FormatSummaryCompleted(snapshot.Root.Name)
            : _localization.FormatSummaryCompletedWithDiagnostics(snapshot.Root.Name, snapshot.Diagnostics.Count);
        TotalsText = _localization.FormatTotalsText(tokenCount, nonEmptyLineCount, fileCount, snapshot.Diagnostics.Count);
        ProgressValue = 0;
        IsProgressIndeterminate = false;
        IsProgressVisible = false;
        IsProgressPillVisible = false;
        ProgressPillText = string.Empty;
        TokenSummaryValue = tokenCount.ToString("N0", CultureInfo.CurrentCulture);
        LineSummaryValue = nonEmptyLineCount.ToString("N0", CultureInfo.CurrentCulture);
        FileSummaryValue = fileCount.ToString("N0", CultureInfo.CurrentCulture);
        WarningSummaryValue = snapshot.Diagnostics.Count.ToString("N0", CultureInfo.CurrentCulture);
    }

    public void UpdateProgress(AnalysisProgress progress)
    {
        _progress = progress;
        if (!_acceptProgressUpdates)
        {
            return;
        }

        IsProgressVisible = true;
        IsProgressPillVisible = true;
        ProgressPillText = BuildProgressPillText(progress);

        if (progress.TotalNodeCount is > 0)
        {
            ProgressValue = Math.Clamp(progress.ProcessedNodeCount * 100d / progress.TotalNodeCount.Value, 0d, 100d);
            IsProgressIndeterminate = false;
        }
        else
        {
            ProgressValue = 0;
            IsProgressIndeterminate = true;
        }
    }

    private void RefreshLocalizedState()
    {
        if (_state == AnalysisState.Completed && _snapshot is not null)
        {
            SetCompleted(_snapshot);
            return;
        }

        SetState(_state);
        if (_state == AnalysisState.Scanning && _progress is not null)
        {
            UpdateProgress(_progress);
        }
    }

    private string BuildProgressPillText(AnalysisProgress progress)
    {
        if (string.Equals(progress.Phase, "AnalyzingFiles", StringComparison.Ordinal) &&
            progress.TotalNodeCount is > 0)
        {
            return _localization.FormatProgressAnalyzingFiles(progress.ProcessedNodeCount, progress.TotalNodeCount.Value);
        }

        if (string.Equals(progress.Phase, "ScanningTree", StringComparison.Ordinal) &&
            progress.DiscoveredFileCount is > 0)
        {
            return _localization.FormatProgressScanningTreeFilesFound(progress.DiscoveredFileCount.Value);
        }

        return string.Equals(progress.Phase, "AnalyzingFiles", StringComparison.Ordinal)
            ? _localization.ProgressAnalyzingFiles
            : _localization.ProgressScanningTree;
    }
}
