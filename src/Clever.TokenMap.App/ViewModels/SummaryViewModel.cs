using Clever.TokenMap.App.Models;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class SummaryViewModel : ViewModelBase
{
    [ObservableProperty]
    private string summaryText = "Select a folder to build a project treemap and metrics snapshot.";

    [ObservableProperty]
    private string totalsText = "No snapshot loaded";

    [ObservableProperty]
    private string progressText = "Waiting for input";

    [ObservableProperty]
    private string statusText = "Idle";

    [ObservableProperty]
    private string tokenSummaryValue = "0";

    [ObservableProperty]
    private string lineSummaryValue = "0";

    [ObservableProperty]
    private string fileSummaryValue = "0";

    [ObservableProperty]
    private string warningSummaryValue = "0";

    public void SetState(AnalysisState state, string? message = null)
    {
        StatusText = state.ToString();

        SummaryText = state switch
        {
            AnalysisState.Idle => "Select a folder to build a project treemap and metrics snapshot.",
            AnalysisState.Scanning => "Analyzing project structure, token counts and line statistics.",
            AnalysisState.Cancelled => "Analysis was cancelled. Previous snapshot remains available.",
            AnalysisState.Failed => "Analysis failed. Check the status message and diagnostics.",
            _ => SummaryText,
        };

        if (!string.IsNullOrWhiteSpace(message))
        {
            ProgressText = message;
        }
    }

    public void SetCompleted(ProjectSnapshot snapshot)
    {
        SummaryText = snapshot.Warnings.Count == 0
            ? $"Analysis completed for {snapshot.Root.Name}."
            : $"Analysis completed for {snapshot.Root.Name} with {snapshot.Warnings.Count:N0} warnings.";
        TotalsText =
            $"{snapshot.Root.Metrics.Tokens:N0} tokens · {snapshot.Root.Metrics.TotalLines:N0} lines · {snapshot.Root.Metrics.DescendantFileCount:N0} files · {snapshot.Warnings.Count:N0} warnings";
        ProgressText = snapshot.Warnings.Count == 0
            ? "Snapshot is ready."
            : "Snapshot is ready. Review warnings before trusting unsupported files.";
        StatusText = AnalysisState.Completed.ToString();
        TokenSummaryValue = snapshot.Root.Metrics.Tokens.ToString("N0");
        LineSummaryValue = snapshot.Root.Metrics.TotalLines.ToString("N0");
        FileSummaryValue = snapshot.Root.Metrics.DescendantFileCount.ToString("N0");
        WarningSummaryValue = snapshot.Warnings.Count.ToString("N0");
    }

    public void UpdateProgress(AnalysisProgress progress)
    {
        var totalText = progress.TotalNodeCount?.ToString() ?? "?";
        var currentPath = string.IsNullOrWhiteSpace(progress.CurrentPath) ? string.Empty : $" · {progress.CurrentPath}";
        ProgressText = $"{progress.Phase}: {progress.ProcessedNodeCount}/{totalText}{currentPath}";
    }
}
