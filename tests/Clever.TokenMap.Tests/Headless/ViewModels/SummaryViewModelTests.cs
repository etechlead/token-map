using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

using Clever.TokenMap.Tests.Headless.Support;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class SummaryViewModelTests
{
    [Fact]
    public void ShowsProgressOnlyWhileAnalysisIsActive()
    {
        var viewModel = new SummaryViewModel();

        Assert.False(viewModel.IsProgressVisible);

        viewModel.SetState(AnalysisState.Scanning);
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.True(viewModel.IsProgressPillVisible);
        Assert.Equal("Scanning tree", viewModel.ProgressPillText);

        viewModel.UpdateProgress(new AnalysisProgress("ScanningTree", 4, null, "src", DiscoveredFileCount: 3));
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.True(viewModel.IsProgressPillVisible);
        Assert.Equal("Scanning tree • 3 files found", viewModel.ProgressPillText);

        viewModel.UpdateProgress(new AnalysisProgress("AnalyzingFiles", 3, 6, "src/Program.cs"));
        Assert.True(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(50, viewModel.ProgressValue);
        Assert.True(viewModel.IsProgressPillVisible);
        Assert.Equal("Analyzing files • 3 / 6", viewModel.ProgressPillText);

        viewModel.SetCompleted(CreateSnapshot());
        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.False(viewModel.IsProgressPillVisible);
        Assert.Equal(string.Empty, viewModel.ProgressPillText);

        viewModel.SetState(AnalysisState.Cancelled);
        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressPillVisible);
    }

    [Fact]
    public void IgnoresLateProgressAfterCompletion()
    {
        var viewModel = new SummaryViewModel();

        viewModel.SetState(AnalysisState.Scanning);
        viewModel.SetCompleted(CreateSnapshot());
        viewModel.UpdateProgress(new AnalysisProgress("AnalyzingFiles", 6, 6, "src/Program.cs"));

        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.False(viewModel.IsProgressPillVisible);
        Assert.Equal(string.Empty, viewModel.ProgressPillText);
    }
}
