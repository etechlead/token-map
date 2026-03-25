using Clever.TokenMap.App;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

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

        viewModel.UpdateProgress(new AnalysisProgress("ScanningTree", 4, null, "src"));
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);

        viewModel.UpdateProgress(new AnalysisProgress("AnalyzingFiles", 3, 6, "src/Program.cs"));
        Assert.True(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(50, viewModel.ProgressValue);

        viewModel.SetCompleted(CreateSnapshot());
        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, viewModel.ProgressValue);

        viewModel.SetState(AnalysisState.Cancelled);
        Assert.False(viewModel.IsProgressVisible);
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
    }
}
