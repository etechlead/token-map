using Clever.TokenMap.App.Models;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class SummaryViewModel : ViewModelBase
{
    [ObservableProperty]
    private string summaryText = "Select a folder and run analysis to populate the tree and project summary.";

    [ObservableProperty]
    private string totalsText = "No snapshot loaded";

    [ObservableProperty]
    private string progressText = "Waiting for input";

    [ObservableProperty]
    private string statusText = "Idle";

    public void SetState(AnalysisState state, string? message = null)
    {
        StatusText = state.ToString();

        if (!string.IsNullOrWhiteSpace(message))
        {
            ProgressText = message;
        }
    }

    public void SetCompleted(ProjectSnapshot snapshot)
    {
        SummaryText = $"Analysis completed for {snapshot.Root.Name}.";
        TotalsText =
            $"{snapshot.Root.Metrics.Tokens:N0} tokens · {snapshot.Root.Metrics.TotalLines:N0} lines · {snapshot.Root.Metrics.DescendantFileCount:N0} files · {snapshot.Warnings.Count:N0} warnings";
        ProgressText = "Snapshot is ready.";
        StatusText = AnalysisState.Completed.ToString();
    }

    public void UpdateProgress(AnalysisProgress progress)
    {
        var totalText = progress.TotalNodeCount?.ToString() ?? "?";
        var currentPath = string.IsNullOrWhiteSpace(progress.CurrentPath) ? string.Empty : $" · {progress.CurrentPath}";
        ProgressText = $"{progress.Phase}: {progress.ProcessedNodeCount}/{totalText}{currentPath}";
    }
}
