using Avalonia;
using Clever.TokenMap.Treemap;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Tests.Treemap;

public sealed class TreemapVisualRulesTests
{
    [Fact]
    public void CanDrawLabel_ReturnsFalse_ForSmallFileTile()
    {
        var node = CreateNode("tiny.cs", ProjectNodeKind.File);

        var canDraw = TreemapVisualRules.CanDrawLabel(node, new Rect(0, 0, 40, 14));

        Assert.False(canDraw);
    }

    [Fact]
    public void CanDrawLabel_ReturnsFalse_ForDirectoryWithoutVisibleHeader()
    {
        var node = CreateNode("src", ProjectNodeKind.Directory);

        var canDraw = TreemapVisualRules.CanDrawLabel(node, new Rect(0, 0, 70, 28));

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

        Assert.Equal(10, TreemapVisualRules.GetLabelFontSize(directory));
        Assert.Equal(12, TreemapVisualRules.GetLabelFontSize(file));
    }

    [Fact]
    public void GetDirectoryHeaderHeight_OnlyAppearsWhenDirectoryHasEnoughSpace()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);
        var hiddenHeight = TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 70, 28));
        var compactHeight = TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 80, 30));
        var roomyHeight = TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 110, 42));

        Assert.Equal(0, hiddenHeight);
        Assert.True(compactHeight > 0);
        Assert.True(roomyHeight >= compactHeight);
        Assert.Equal(0, TreemapVisualRules.GetDirectoryHeaderHeight(file, new Rect(0, 0, 110, 42)));
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

        Assert.True(TreemapVisualRules.CanDrawLabel(directory, new Rect(0, 0, 110, 42)));
        Assert.True(TreemapVisualRules.CanDrawLabel(file, new Rect(0, 0, 90, 32)));
    }

    [Fact]
    public void GetHeaderBounds_ForDirectory_UsesInsetAndVisibleHeight()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var bounds = new Rect(10, 20, 80, 30);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(directory, bounds);
        var contentBounds = TreemapVisualRules.GetContentBounds(directory, bounds);

        Assert.True(headerBounds.Width > 0);
        Assert.True(headerBounds.Height > 0);
        Assert.Equal(bounds.X + 2, headerBounds.X);
        Assert.Equal(bounds.Y + 2, headerBounds.Y);
        Assert.True(headerBounds.Bottom <= contentBounds.Y);
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
            Metrics = new NodeMetrics(
                Tokens: 100,
                NonEmptyLines: 20,
                FileSizeBytes: 100,
                DescendantFileCount: kind == ProjectNodeKind.File ? 1 : 0,
                DescendantDirectoryCount: kind == ProjectNodeKind.Directory ? 1 : 0),
        };
}

