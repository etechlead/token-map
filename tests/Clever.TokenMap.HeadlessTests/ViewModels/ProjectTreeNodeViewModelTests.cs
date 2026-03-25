using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

public sealed class ProjectTreeNodeViewModelTests
{
    [Fact]
    public void ShowsNaForSkippedAnalysisMetrics()
    {
        var parentNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
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
            FullPath = "C:\\Demo\\image.ico",
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
        Assert.Equal("167.8 KB", skippedFileNode.SizeText);
        Assert.Equal("n/a", skippedFileNode.ParentShareText);
        Assert.Null(skippedFileNode.ParentShareRatio);
    }

    [Fact]
    public void ParentShare_UsesImmediateParentMetric()
    {
        var rootNode = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
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
            FullPath = "C:\\Demo\\Alpha.cs",
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
            FullPath = "C:\\Zero",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = NodeMetrics.Empty,
        };
        var childWithZeroParent = new ProjectTreeNodeViewModel(
            childNode,
            parentNode: zeroMetricParent,
            parentShareMetric: AnalysisMetric.Tokens);

        Assert.Equal("100.0%", rootViewModel.ParentShareText);
        Assert.NotNull(childViewModel.ParentShareRatio);
        Assert.Equal(1d / 3d, childViewModel.ParentShareRatio.Value, 3);
        Assert.Equal("33.3%", childViewModel.ParentShareText);
        Assert.Equal("n/a", childWithZeroParent.ParentShareText);
        Assert.Null(childWithZeroParent.ParentShareRatio);
    }
}
