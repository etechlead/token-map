using System.Globalization;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
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
            Metrics = new NodeMetrics(
                Tokens: 100,
                NonEmptyLines: 50,
                FileSizeBytes: 171_801,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var skippedFileNode = new ProjectTreeNodeViewModel(new ProjectNode
        {
            Id = "image.ico",
            Name = "image.ico",
            FullPath = TestPaths.CombineUnder(demoRootPath, "image.ico"),
            RelativePath = "image.ico",
            Kind = ProjectNodeKind.File,
            SkippedReason = SkippedReason.Binary,
            Metrics = new NodeMetrics(
                Tokens: 0,
                NonEmptyLines: 0,
                FileSizeBytes: 171_801,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        },
        parentNode: parentNode,
        parentShareMetric: AnalysisMetric.Tokens);

        Assert.Equal("n/a", skippedFileNode.TokensText);
        Assert.Equal("n/a", skippedFileNode.LinesText);
        Assert.Equal($"167{decimalSeparator}8 KB", skippedFileNode.SizeText);
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
            Metrics = new NodeMetrics(
                Tokens: 30,
                NonEmptyLines: 12,
                FileSizeBytes: 90,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var childNode = new ProjectNode
        {
            Id = "Alpha.cs",
            Name = "Alpha.cs",
            FullPath = TestPaths.CombineUnder(demoRootPath, "Alpha.cs"),
            RelativePath = "Alpha.cs",
            Kind = ProjectNodeKind.File,
            Metrics = new NodeMetrics(
                Tokens: 10,
                NonEmptyLines: 4,
                FileSizeBytes: 30,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
        var rootViewModel = new ProjectTreeNodeViewModel(rootNode);
        var childViewModel = new ProjectTreeNodeViewModel(
            childNode,
            depth: 1,
            parentNode: rootNode,
            parentShareMetric: AnalysisMetric.Tokens);
        var zeroMetricParent = new ProjectNode
        {
            Id = "/zero",
            Name = "Zero",
            FullPath = zeroRootPath,
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = NodeMetrics.Empty,
        };
        var childWithZeroParent = new ProjectTreeNodeViewModel(
            childNode,
            parentNode: zeroMetricParent,
            parentShareMetric: AnalysisMetric.Tokens);

        Assert.Equal($"100{decimalSeparator}0%", rootViewModel.ParentShareText);
        Assert.NotNull(childViewModel.ParentShareRatio);
        Assert.Equal(1d / 3d, childViewModel.ParentShareRatio.Value, 3);
        Assert.Equal($"33{decimalSeparator}3%", childViewModel.ParentShareText);
        Assert.Equal("n/a", childWithZeroParent.ParentShareText);
        Assert.Null(childWithZeroParent.ParentShareRatio);
    }
}
