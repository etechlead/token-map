using Clever.TokenMap.App.Models;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

public sealed class AnalysisSessionControllerTests
{
    [Fact]
    public async Task OpenFolderAsync_CompletesAnalysisAndStoresSnapshot()
    {
        var snapshot = CreateSnapshot("Initial");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer((_, _, _, _) => Task.FromResult(snapshot)),
            new FixedFolderPickerService("C:\\Demo"));

        await controller.OpenFolderAsync(ScanOptions.Default);

        Assert.Equal("C:\\Demo", controller.SelectedFolderPath);
        Assert.Equal(AnalysisState.Completed, controller.State);
        Assert.True(controller.HasSnapshot);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);
    }

    [Fact]
    public async Task RescanAsync_ReplacesExistingSnapshot()
    {
        var first = CreateSnapshot("First");
        var second = CreateSnapshot("Second");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                (_, _, _, _) => Task.FromResult(first),
                (_, _, _, _) => Task.FromResult(second)),
            new FixedFolderPickerService("C:\\Demo"));

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.RescanAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Completed, controller.State);
        Assert.Equal("Second", controller.CurrentSnapshot?.Root.Name);
    }

    [Fact]
    public async Task Cancel_AllowsSubsequentRescan()
    {
        var recovered = CreateSnapshot("Recovered");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                async (_, _, progress, cancellationToken) =>
                {
                    progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "src"));
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return CreateSnapshot("Cancelled");
                },
                (_, _, _, _) => Task.FromResult(recovered)),
            new FixedFolderPickerService("C:\\Demo"));

        var openTask = controller.OpenFolderAsync(ScanOptions.Default);
        await WaitForStateAsync(controller, AnalysisState.Scanning);

        controller.Cancel();
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, controller.State);
        Assert.False(controller.IsBusy);
        Assert.Null(controller.CurrentProgress);

        await controller.RescanAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Completed, controller.State);
        Assert.Equal("Recovered", controller.CurrentSnapshot?.Root.Name);
    }

    [Fact]
    public async Task Failure_KeepsPreviousSnapshot()
    {
        var initial = CreateSnapshot("Initial");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                (_, _, _, _) => Task.FromResult(initial),
                (_, _, _, _) => Task.FromException<ProjectSnapshot>(new InvalidOperationException("boom"))),
            new FixedFolderPickerService("C:\\Demo"));

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.RescanAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Failed, controller.State);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);
        Assert.Equal("boom", controller.StatusMessage);
    }

    private static async Task WaitForStateAsync(AnalysisSessionController controller, AnalysisState expectedState)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (controller.State == expectedState)
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Equal(expectedState, controller.State);
    }

    private static ProjectSnapshot CreateSnapshot(string name) =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = name,
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    TotalLines: 12,
                    NonEmptyLines: 11,
                    BlankLines: 1,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
            },
        };

    private sealed class FixedFolderPickerService(string path) : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(path);
    }

    private sealed class SequenceProjectAnalyzer(params Func<string, ScanOptions, IProgress<AnalysisProgress>?, CancellationToken, Task<ProjectSnapshot>>[] handlers) : IProjectAnalyzer
    {
        private readonly Queue<Func<string, ScanOptions, IProgress<AnalysisProgress>?, CancellationToken, Task<ProjectSnapshot>>> _handlers = new(handlers);

        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (_handlers.Count == 0)
            {
                throw new InvalidOperationException("No more analyzer runs configured.");
            }

            return _handlers.Dequeue()(rootPath, options, progress, cancellationToken);
        }
    }
}
