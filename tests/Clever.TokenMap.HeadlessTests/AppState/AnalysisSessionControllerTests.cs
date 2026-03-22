using Clever.TokenMap.App;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using System.Collections.Generic;

namespace Clever.TokenMap.HeadlessTests;

public sealed class AnalysisSessionControllerTests
{
    [Fact]
    public async Task OpenFolderAsync_CompletesAnalysisAndStoresSnapshot()
    {
        var snapshot = CreateSnapshot("Initial");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer((_, _, _, _) => Task.FromResult(snapshot)),
            new FixedFolderPickerService("C:\\Demo"),
            new FixedFolderPathService());

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
            new FixedFolderPickerService("C:\\Demo"),
            new FixedFolderPathService());

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.RescanAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Completed, controller.State);
        Assert.Equal("Second", controller.CurrentSnapshot?.Root.Name);
    }

    [Fact]
    public async Task Cancel_InitialOpen_DoesNotCommitFolderOrSnapshot()
    {
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                async (_, _, progress, cancellationToken) =>
                {
                    progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "src"));
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return CreateSnapshot("Cancelled");
                }),
            new FixedFolderPickerService("C:\\Demo"),
            new FixedFolderPathService());

        var openTask = controller.OpenFolderAsync(ScanOptions.Default);
        await WaitForStateAsync(controller, AnalysisState.Scanning);

        controller.Cancel();
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, controller.State);
        Assert.False(controller.IsBusy);
        Assert.False(controller.HasSelectedFolder);
        Assert.False(controller.HasSnapshot);
        Assert.Null(controller.CurrentProgress);
        Assert.Null(controller.SelectedFolderPath);
        Assert.Null(controller.CurrentSnapshot);
    }

    [Fact]
    public async Task OpenFolderAsync_CancelForNewFolder_KeepsPreviousCommittedSelectionAndSnapshot()
    {
        var initial = CreateSnapshot("Initial", rootPath: "C:\\RepoA");
        var recovered = CreateSnapshot("Recovered", rootPath: "C:\\RepoA");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                (_, _, _, _) => Task.FromResult(initial),
                async (_, _, progress, cancellationToken) =>
                {
                    progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "src"));
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return CreateSnapshot("Cancelled", rootPath: "C:\\RepoB");
                },
                (_, _, _, _) => Task.FromResult(recovered)),
            new SequenceFolderPickerService("C:\\RepoA", "C:\\RepoB"),
            new FixedFolderPathService());

        await controller.OpenFolderAsync(ScanOptions.Default);
        var reopenTask = controller.OpenFolderAsync(ScanOptions.Default);
        await WaitForStateAsync(controller, AnalysisState.Scanning);

        controller.Cancel();
        await reopenTask;

        Assert.Equal(AnalysisState.Cancelled, controller.State);
        Assert.Equal("C:\\RepoA", controller.SelectedFolderPath);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);

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
            new FixedFolderPickerService("C:\\Demo"),
            new FixedFolderPathService());

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.RescanAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Failed, controller.State);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);
        Assert.Equal("boom", controller.StatusMessage);
    }

    [Fact]
    public async Task OpenFolderAsync_FailureForNewFolder_KeepsPreviousCommittedSelectionAndSnapshot()
    {
        var initial = CreateSnapshot("Initial", rootPath: "C:\\RepoA");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer(
                (_, _, _, _) => Task.FromResult(initial),
                (_, _, _, _) => Task.FromException<ProjectSnapshot>(new InvalidOperationException("boom"))),
            new SequenceFolderPickerService("C:\\RepoA", "C:\\RepoB"),
            new FixedFolderPathService());

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.OpenFolderAsync(ScanOptions.Default);

        Assert.Equal(AnalysisState.Failed, controller.State);
        Assert.Equal("C:\\RepoA", controller.SelectedFolderPath);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);
        Assert.Equal("boom", controller.StatusMessage);
    }

    [Fact]
    public async Task OpenFolderAsync_WithExplicitPath_CompletesAnalysisAndStoresSnapshot()
    {
        var snapshot = CreateSnapshot("Recent", rootPath: "C:\\RecentRepo");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer((_, _, _, _) => Task.FromResult(snapshot)),
            new FixedFolderPickerService("C:\\Ignored"),
            new FixedFolderPathService());

        await controller.OpenFolderAsync("C:\\RecentRepo", ScanOptions.Default);

        Assert.Equal("C:\\RecentRepo", controller.SelectedFolderPath);
        Assert.Equal(AnalysisState.Completed, controller.State);
        Assert.Equal("Recent", controller.CurrentSnapshot?.Root.Name);
    }

    [Fact]
    public async Task OpenFolderAsync_MissingFolder_KeepsPreviousCommittedSelectionAndSnapshot()
    {
        var initial = CreateSnapshot("Initial", rootPath: "C:\\RepoA");
        var controller = new AnalysisSessionController(
            new SequenceProjectAnalyzer((_, _, _, _) => Task.FromResult(initial)),
            new FixedFolderPickerService("C:\\RepoA"),
            new FixedFolderPathService(existingPaths: ["C:\\RepoA"]));

        await controller.OpenFolderAsync(ScanOptions.Default);
        await controller.OpenFolderAsync("C:\\RepoB", ScanOptions.Default);

        Assert.Equal(AnalysisState.Failed, controller.State);
        Assert.Equal("Project root was not found: C:\\RepoB", controller.StatusMessage);
        Assert.Equal("C:\\RepoA", controller.SelectedFolderPath);
        Assert.Equal("Initial", controller.CurrentSnapshot?.Root.Name);
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

    private static ProjectSnapshot CreateSnapshot(string name, string rootPath = "C:\\Demo") =>
        new()
        {
            RootPath = rootPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = name,
                FullPath = rootPath,
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

    private sealed class SequenceFolderPickerService(params string[] paths) : IFolderPickerService
    {
        private readonly Queue<string> _paths = new(paths);

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
        {
            if (_paths.Count == 0)
            {
                throw new InvalidOperationException("No more folder picker results configured.");
            }

            return Task.FromResult<string?>(_paths.Dequeue());
        }
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

    private sealed class FixedFolderPathService(IEnumerable<string>? existingPaths = null) : IFolderPathService
    {
        private readonly HashSet<string> _existingPaths = existingPaths is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);

        public bool Exists(string folderPath)
        {
            return _existingPaths.Count == 0 || _existingPaths.Contains(folderPath);
        }
    }
}
