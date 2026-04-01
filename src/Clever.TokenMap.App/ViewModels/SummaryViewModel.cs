using System;
using System.Globalization;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class SummaryViewModel : ViewModelBase, ISummaryProjection
{
    private bool _acceptProgressUpdates;

    [ObservableProperty]
    private string summaryText = "Select a folder to build a project treemap and metrics snapshot.";

    [ObservableProperty]
    private string totalsText = "No snapshot loaded";

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
        SummaryText = state switch
        {
            AnalysisState.Idle => "Select a folder to build a project treemap and metrics snapshot.",
            AnalysisState.Scanning => "Analyzing project structure, token counts and non-empty line statistics.",
            AnalysisState.Cancelled => "Analysis was cancelled. Previous snapshot remains available.",
            AnalysisState.Failed => "Analysis failed. Review the diagnostics issue and log details.",
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
                ProgressPillText = "Scanning tree";
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
        var tokenCount = snapshot.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens) ?? 0;
        var nonEmptyLineCount = snapshot.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.NonEmptyLines) ?? 0;
        var fileCount = snapshot.Root.Summary.DescendantFileCount;
        _acceptProgressUpdates = false;
        SummaryText = snapshot.Diagnostics.Count == 0
            ? $"Analysis completed for {snapshot.Root.Name}."
            : $"Analysis completed for {snapshot.Root.Name} with {snapshot.Diagnostics.Count:N0} diagnostics.";
        TotalsText =
            $"{tokenCount:N0} tokens - {nonEmptyLineCount:N0} non-empty lines - {fileCount:N0} files - {snapshot.Diagnostics.Count:N0} diagnostics";
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

    private static string BuildProgressPillText(AnalysisProgress progress)
    {
        if (string.Equals(progress.Phase, "AnalyzingFiles", StringComparison.Ordinal) &&
            progress.TotalNodeCount is > 0)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "Analyzing files • {0:N0} / {1:N0}",
                progress.ProcessedNodeCount,
                progress.TotalNodeCount.Value);
        }

        if (string.Equals(progress.Phase, "ScanningTree", StringComparison.Ordinal) &&
            progress.DiscoveredFileCount is > 0)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "Scanning tree • {0:N0} files found",
                progress.DiscoveredFileCount.Value);
        }

        return string.Equals(progress.Phase, "AnalyzingFiles", StringComparison.Ordinal)
            ? "Analyzing files"
            : "Scanning tree";
    }
}
