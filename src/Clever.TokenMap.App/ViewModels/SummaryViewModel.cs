using System;
using System.Globalization;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class SummaryViewModel : ViewModelBase
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
            AnalysisState.Failed => "Analysis failed. Check the status message and diagnostics.",
            _ => SummaryText,
        };

        switch (state)
        {
            case AnalysisState.Scanning:
                _acceptProgressUpdates = true;
                ProgressValue = 0;
                IsProgressIndeterminate = true;
                IsProgressVisible = true;
                break;
            default:
                _acceptProgressUpdates = false;
                ProgressValue = 0;
                IsProgressIndeterminate = false;
                IsProgressVisible = false;
                break;
        }
    }

    public void SetCompleted(ProjectSnapshot snapshot)
    {
        _acceptProgressUpdates = false;
        SummaryText = snapshot.Warnings.Count == 0
            ? $"Analysis completed for {snapshot.Root.Name}."
            : $"Analysis completed for {snapshot.Root.Name} with {snapshot.Warnings.Count:N0} warnings.";
        TotalsText =
            $"{snapshot.Root.Metrics.Tokens:N0} tokens - {snapshot.Root.Metrics.NonEmptyLines:N0} non-empty lines - {snapshot.Root.Metrics.DescendantFileCount:N0} files - {snapshot.Warnings.Count:N0} warnings";
        ProgressValue = 0;
        IsProgressIndeterminate = false;
        IsProgressVisible = false;
        TokenSummaryValue = snapshot.Root.Metrics.Tokens.ToString("N0", CultureInfo.CurrentCulture);
        LineSummaryValue = snapshot.Root.Metrics.NonEmptyLines.ToString("N0", CultureInfo.CurrentCulture);
        FileSummaryValue = snapshot.Root.Metrics.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture);
        WarningSummaryValue = snapshot.Warnings.Count.ToString("N0", CultureInfo.CurrentCulture);
    }

    public void UpdateProgress(AnalysisProgress progress)
    {
        if (!_acceptProgressUpdates)
        {
            return;
        }

        IsProgressVisible = true;

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
}
