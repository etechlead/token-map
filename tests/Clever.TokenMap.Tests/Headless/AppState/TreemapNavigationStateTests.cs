using System.Linq;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Support;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class TreemapNavigationStateTests
{
    [Fact]
    public void LoadSnapshot_InitializesSelectionAndBreadcrumbs()
    {
        var snapshot = CreateNestedSnapshot();
        var state = new TreemapNavigationState();

        state.LoadSnapshot(snapshot);

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(snapshot.Root, state.SelectedNode);
        Assert.Single(state.TreemapBreadcrumbs);
        Assert.Equal("Demo", state.TreemapBreadcrumbs[0].Label);
        Assert.False(state.CanResetTreemapRoot);
    }

    [Fact]
    public void DrillInto_UpdatesRootSelectionAndBreadcrumbs()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);

        var handled = state.DrillInto(directory);

        Assert.True(handled);
        Assert.Equal(directory, state.TreemapRootNode);
        Assert.Equal(directory, state.SelectedNode);
        Assert.Equal(2, state.TreemapBreadcrumbs.Count);
        Assert.True(state.CanResetTreemapRoot);
        Assert.Equal("/ src", state.TreemapBreadcrumbs[1].Label);
    }

    [Fact]
    public void CanSetTreemapRoot_RejectsCurrentRootAndFiles()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var file = Assert.Single(directory.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);

        Assert.False(state.CanSetTreemapRoot(snapshot.Root));
        Assert.True(state.CanSetTreemapRoot(directory));
        Assert.False(state.CanSetTreemapRoot(file));

        state.SetTreemapRoot(directory);

        Assert.False(state.CanSetTreemapRoot(directory));
    }

    [Fact]
    public void ResetAndBreadcrumbNavigation_RestoreOverviewWithoutClearingSelection()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var file = Assert.Single(directory.Children);
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);
        state.DrillInto(directory);
        state.SelectNode(file);

        state.NavigateToBreadcrumb(snapshot.Root);

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(file, state.SelectedNode);
        Assert.Single(state.TreemapBreadcrumbs);

        state.DrillInto(directory);
        state.ResetTreemapRoot();

        Assert.Equal(snapshot.Root, state.TreemapRootNode);
        Assert.Equal(directory, state.SelectedNode);
        Assert.False(state.CanResetTreemapRoot);
    }

    [Fact]
    public void LoadSnapshot_InitializesThresholdRangeFromCurrentMetric()
    {
        var snapshot = CreateThresholdSnapshot();
        var state = new TreemapNavigationState();

        state.LoadSnapshot(snapshot);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.Equal(2, state.ThresholdSliderMaximum);
        Assert.Equal(0, state.ThresholdSliderValue);
        Assert.Equal(5, state.ThresholdValue);
        Assert.True(state.CanAdjustThreshold);
    }

    [Fact]
    public void SetSelectedMetricAndTreemapRoot_RecomputeThresholdRangeForCurrentScope()
    {
        var snapshot = CreateThresholdScopedSnapshot();
        var rootDirectory = Assert.Single(snapshot.Root.Children);
        var nestedDirectory = Assert.Single(rootDirectory.Children, child => child.Kind == ProjectNodeKind.Directory);
        var state = new TreemapNavigationState();

        state.LoadSnapshot(snapshot);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.Equal(2, state.ThresholdSliderMaximum);
        Assert.Equal(8, state.ThresholdValue);

        state.DrillInto(rootDirectory);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.Equal(2, state.ThresholdSliderMaximum);
        Assert.Equal(8, state.ThresholdValue);

        state.SetSelectedMetric(MetricIds.NonEmptyLines);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.Equal(1, state.ThresholdSliderMaximum);
        Assert.Equal(10, state.ThresholdValue);

        state.DrillInto(nestedDirectory);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.Equal(0, state.ThresholdSliderMaximum);
        Assert.Equal(70, state.ThresholdValue);
        Assert.False(state.CanAdjustThreshold);
    }

    [Fact]
    public void LoadSnapshot_CapsThresholdStepsToRepresentativeBuckets()
    {
        var snapshot = CreateLargeThresholdSnapshot(fileCount: 1000);
        var state = new TreemapNavigationState();

        state.LoadSnapshot(snapshot);

        Assert.Equal(0, state.ThresholdSliderMinimum);
        Assert.True(state.ThresholdSliderMaximum <= 255);
        Assert.Equal(1, state.ThresholdValue);

        state.ThresholdSliderValue = state.ThresholdSliderMaximum;

        Assert.Equal(1000, state.ThresholdValue);
    }

    [Fact]
    public void AdjustThresholdStep_MovesOneDiscreteStepAndClampsAtEdges()
    {
        var snapshot = CreateThresholdSnapshot();
        var state = new TreemapNavigationState();
        state.LoadSnapshot(snapshot);

        var increased = state.AdjustThresholdStep(1);
        var clampedHigh = state.AdjustThresholdStep(10);
        var clampedLow = state.AdjustThresholdStep(-10);

        Assert.True(increased);
        Assert.True(clampedHigh);
        Assert.True(clampedLow);
        Assert.Equal(0, state.ThresholdSliderValue);
        Assert.Equal(5, state.ThresholdValue);

        state.AdjustThresholdStep(2);

        Assert.False(state.AdjustThresholdStep(1));
        Assert.Equal(80, state.ThresholdValue);
    }

    private static ProjectSnapshot CreateThresholdSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = CreateRootWithChildren(
                ("a.cs", FileSizeBytes: 50, Tokens: 80, NonEmptyLines: 10),
                ("b.cs", FileSizeBytes: 75, Tokens: 20, NonEmptyLines: 90),
                ("c.cs", FileSizeBytes: 225, Tokens: 5, NonEmptyLines: 0)),
        };

    private static ProjectSnapshot CreateThresholdScopedSnapshot()
    {
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 3, descendantDirectoryCount: 2),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 138, nonEmptyLines: 80, fileSizeBytes: 390),
        };

        var src = new ProjectNode
        {
            Id = "src",
            Name = "src",
            FullPath = "C:\\Demo\\src",
            RelativePath = "src",
            Kind = ProjectNodeKind.Directory,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 3, descendantDirectoryCount: 1),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 138, nonEmptyLines: 80, fileSizeBytes: 390),
        };

        var nested = new ProjectNode
        {
            Id = "src/core",
            Name = "core",
            FullPath = "C:\\Demo\\src\\core",
            RelativePath = "src/core",
            Kind = ProjectNodeKind.Directory,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 90, nonEmptyLines: 70, fileSizeBytes: 210),
        };

        nested.Children.Add(new ProjectNode
        {
            Id = "src/core/Engine.cs",
            Name = "Engine.cs",
            FullPath = "C:\\Demo\\src\\core\\Engine.cs",
            RelativePath = "src/core/Engine.cs",
            Kind = ProjectNodeKind.File,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 90, nonEmptyLines: 70, fileSizeBytes: 210),
        });

        src.Children.Add(new ProjectNode
        {
            Id = "src/App.cs",
            Name = "App.cs",
            FullPath = "C:\\Demo\\src\\App.cs",
            RelativePath = "src/App.cs",
            Kind = ProjectNodeKind.File,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 40, nonEmptyLines: 10, fileSizeBytes: 90),
        });
        src.Children.Add(new ProjectNode
        {
            Id = "src/View.axaml",
            Name = "View.axaml",
            FullPath = "C:\\Demo\\src\\View.axaml",
            RelativePath = "src/View.axaml",
            Kind = ProjectNodeKind.File,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 8, nonEmptyLines: 0, fileSizeBytes: 90),
        });
        src.Children.Add(nested);
        root.Children.Add(src);

        return new ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = root,
        };
    }

    private static ProjectSnapshot CreateLargeThresholdSnapshot(int fileCount)
    {
        var root = CreateRootWithChildren(
            [.. Enumerable.Range(1, fileCount)
                .Select(index => ($"File-{index:D4}.cs", FileSizeBytes: (long)index, Tokens: index, NonEmptyLines: index))]);

        return new ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = root,
        };
    }
}
