using Avalonia;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Tests.Support;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.Tests.Treemap;

public sealed class TreemapVisualRulesTests
{
    [Fact]
    public void CanDrawLabel_ReturnsFalse_ForTinyFileTile()
    {
        var node = CreateNode("tiny.cs", ProjectNodeKind.File);

        var canDraw = TreemapVisualRules.CanDrawLabel(node, new Rect(0, 0, 20, 10));

        Assert.False(canDraw);
    }

    [Fact]
    public void CanDrawLabel_ReturnsFalse_ForTinyDirectoryTile()
    {
        var node = CreateNode("src", ProjectNodeKind.Directory);

        var canDraw = TreemapVisualRules.CanDrawLabel(node, new Rect(0, 0, 24, 12));

        Assert.False(canDraw);
    }

    [Fact]
    public void GetLabelBounds_ForDirectory_StaysWithinHeaderBounds()
    {
        var node = CreateNode("src", ProjectNodeKind.Directory);
        var bounds = new Rect(10, 20, 160, 80);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(node, bounds);
        var labelBounds = TreemapVisualRules.GetLabelBounds(node, bounds);

        Assert.True(headerBounds.Width > 0);
        Assert.True(headerBounds.Height > 0);
        Assert.True(labelBounds.Width > 0);
        Assert.True(labelBounds.Height > 0);
        Assert.True(labelBounds.X >= headerBounds.X);
        Assert.True(labelBounds.Y >= headerBounds.Y);
        Assert.True(labelBounds.Right <= headerBounds.Right);
        Assert.True(labelBounds.Bottom <= headerBounds.Bottom);
    }

    [Fact]
    public void GetLabelFontSize_ReturnsSmallerFont_ForDirectoryHeaders()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        var directoryFontSize = TreemapVisualRules.GetLabelFontSize(directory);
        var fileFontSize = TreemapVisualRules.GetLabelFontSize(file);

        Assert.True(directoryFontSize > 0);
        Assert.True(fileFontSize > 0);
        Assert.True(directoryFontSize < fileFontSize);
    }

    [Fact]
    public void GetDirectoryHeaderHeight_OnlyAppearsWhenDirectoryHasEnoughSpace()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);
        var hiddenHeight = TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 24, 12));
        var roomyHeight = TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 200, 100));

        Assert.Equal(0, hiddenHeight);
        Assert.True(roomyHeight > 0);
        Assert.Equal(0, TreemapVisualRules.GetDirectoryHeaderHeight(file, new Rect(0, 0, 200, 100)));
    }

    [Fact]
    public void GetContentBounds_ForDirectory_StartsBelowHeaderBounds()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var bounds = new Rect(10, 20, 80, 30);
        var headerBounds = TreemapVisualRules.GetHeaderBounds(directory, bounds);

        var contentBounds = TreemapVisualRules.GetContentBounds(directory, bounds);

        Assert.Equal(headerBounds.X, contentBounds.X);
        Assert.Equal(headerBounds.Width, contentBounds.Width);
        Assert.Equal(headerBounds.Bottom, contentBounds.Y);
        Assert.True(contentBounds.Height >= 0);
    }

    [Fact]
    public void GetContentBounds_WithoutDirectoryHeader_UsesFullDirectoryBounds()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var bounds = new Rect(10, 20, 80, 30);

        var contentBounds = TreemapVisualRules.GetContentBounds(directory, bounds, includeDirectoryHeader: false);

        Assert.Equal(bounds, contentBounds);
    }

    [Fact]
    public void GetHeaderBounds_ReturnsDefault_ForNonDirectory()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(file, new Rect(10, 20, 160, 80));

        Assert.Equal(default, headerBounds);
    }

    [Fact]
    public void GetLabelBounds_ForFiles_StaysWithinInsetArea()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);
        var bounds = new Rect(10, 20, 100, 40);
        var insetBounds = TreemapVisualRules.Inset(bounds, 1);

        var labelBounds = TreemapVisualRules.GetLabelBounds(file, bounds);

        Assert.True(labelBounds.Width > 0);
        Assert.True(labelBounds.Height > 0);
        Assert.True(labelBounds.X >= insetBounds.X);
        Assert.True(labelBounds.Y >= insetBounds.Y);
        Assert.True(labelBounds.Right <= insetBounds.Right);
        Assert.True(labelBounds.Bottom <= insetBounds.Bottom);
    }

    [Fact]
    public void CanDrawLabel_ReturnsTrue_ForComfortablySizedTiles()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        Assert.True(TreemapVisualRules.CanDrawLabel(directory, new Rect(0, 0, 200, 100)));
        Assert.True(TreemapVisualRules.CanDrawLabel(file, new Rect(0, 0, 200, 100)));
    }

    [Fact]
    public void CanDrawMetricValueLabel_ReturnsFalse_ForTinyFileTile()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        Assert.False(TreemapVisualRules.CanDrawMetricValueLabel(file, new Rect(0, 0, 20, 10)));
    }

    [Fact]
    public void CanDrawMetricValueLabel_ReturnsTrue_ForRoomyFileTile()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        Assert.True(TreemapVisualRules.CanDrawMetricValueLabel(file, new Rect(0, 0, 200, 100)));
    }

    [Fact]
    public void Inset_ReturnsOriginalRect_WhenRectIsTooSmall()
    {
        var rect = new Rect(10, 20, 1, 1);

        var inset = TreemapVisualRules.Inset(rect, 1);

        Assert.Equal(rect, inset);
    }

    private static ProjectNode CreateNode(string relativePath, ProjectNodeKind kind) =>
        new()
        {
            Id = relativePath,
            Name = Path.GetFileName(relativePath),
            FullPath = $"C:\\root\\{relativePath.Replace('/', '\\')}",
            RelativePath = relativePath,
            Kind = kind,
            Summary = kind == ProjectNodeKind.File
                ? MetricTestData.CreateFileSummary()
                : MetricTestData.CreateDirectorySummary(descendantFileCount: 0, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 100, nonEmptyLines: 20, fileSizeBytes: 100),
        };
}
