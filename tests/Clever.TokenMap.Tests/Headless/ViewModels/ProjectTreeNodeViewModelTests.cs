using System.Globalization;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class ProjectTreeNodeViewModelTests
{
    [Fact]
    public void ShowsNaForSkippedAnalysisMetrics()
    {
        var demoRootPath = TestPaths.Folder("Demo");
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var parentNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = demoRootPath,
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 100, nonEmptyLines: 50, fileSizeBytes: 171_801),
        };
        var skippedFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "image.ico",
            Name = "image.ico",
            FullPath = TestPaths.CombineUnder(demoRootPath, "image.ico"),
            RelativePath = "image.ico",
            Kind = ProjectNodeKind.File,
            SkippedReason = SkippedReason.Binary,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateSkippedComputedMetrics(fileSizeBytes: 171_801),
        },
        parentNode: parentNode,
        parentShareMetric: MetricIds.Tokens);

        Assert.Equal("n/a", skippedFileNode.GetMetricText(MetricIds.Tokens));
        Assert.Equal("n/a", skippedFileNode.GetMetricText(MetricIds.NonEmptyLines));
        Assert.Equal($"167{decimalSeparator}8 KB", skippedFileNode.GetMetricText(MetricIds.FileSizeBytes));
        Assert.Equal("n/a", skippedFileNode.ParentShareText);
        Assert.Null(skippedFileNode.ParentShareRatio);
    }

    [Fact]
    public void ParentShare_UsesImmediateParentMetric()
    {
        var demoRootPath = TestPaths.Folder("Demo");
        var zeroRootPath = TestPaths.Folder("Zero");
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var rootNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = demoRootPath,
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 30, nonEmptyLines: 12, fileSizeBytes: 90),
        };
        var childNode = new ProjectNode
        {
            Id = "Alpha.cs",
            Name = "Alpha.cs",
            FullPath = TestPaths.CombineUnder(demoRootPath, "Alpha.cs"),
            RelativePath = "Alpha.cs",
            Kind = ProjectNodeKind.File,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 10, nonEmptyLines: 4, fileSizeBytes: 30),
        };
        var rootViewModel = new ProjectTreeNodeViewModel(rootNode);
        var childViewModel = new ProjectTreeNodeViewModel(
            childNode,
            depth: 1,
            parentNode: rootNode,
            parentShareMetric: MetricIds.Tokens);
        var zeroMetricParent = new ProjectNode
        {
            Id = "/zero",
            Name = "Zero",
            FullPath = zeroRootPath,
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = NodeSummary.Empty,
            ComputedMetrics = MetricSet.Empty,
        };
        var childWithZeroParent = new ProjectTreeNodeViewModel(
            childNode,
            parentNode: zeroMetricParent,
            parentShareMetric: MetricIds.Tokens);

        Assert.Equal($"100{decimalSeparator}0%", rootViewModel.ParentShareText);
        Assert.NotNull(childViewModel.ParentShareRatio);
        Assert.Equal(1d / 3d, childViewModel.ParentShareRatio.Value, 3);
        Assert.Equal($"33{decimalSeparator}3%", childViewModel.ParentShareText);
        Assert.Equal("n/a", childWithZeroParent.ParentShareText);
        Assert.Null(childWithZeroParent.ParentShareRatio);
    }
}
