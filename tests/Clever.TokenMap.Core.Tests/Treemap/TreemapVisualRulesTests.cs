using Avalonia;
using Clever.TokenMap.Treemap;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Tests.Treemap;

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
    public void GetLabelBounds_UsesDirectoryHeaderArea()
    {
        var node = CreateNode("src", ProjectNodeKind.Directory);

        var labelBounds = TreemapVisualRules.GetLabelBounds(node, new Rect(10, 20, 160, 80));

        Assert.Equal(15, labelBounds.X);
        Assert.Equal(22, labelBounds.Y);
        Assert.Equal(150, labelBounds.Width);
        Assert.Equal(16, labelBounds.Height);
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
    public void GetDirectoryHeaderHeight_UsesExpectedThresholds()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        Assert.Equal(12, TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 80, 30)));
        Assert.Equal(16, TreemapVisualRules.GetDirectoryHeaderHeight(directory, new Rect(0, 0, 110, 42)));
        Assert.Equal(0, TreemapVisualRules.GetDirectoryHeaderHeight(file, new Rect(0, 0, 110, 42)));
    }

    [Fact]
    public void GetContentBounds_ExcludesVisibleDirectoryHeader()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);

        var contentBounds = TreemapVisualRules.GetContentBounds(directory, new Rect(10, 20, 80, 30));

        Assert.Equal(12, contentBounds.X);
        Assert.Equal(34, contentBounds.Y);
        Assert.Equal(76, contentBounds.Width);
        Assert.Equal(14, contentBounds.Height);
    }

    [Fact]
    public void GetHeaderBounds_ReturnsDefault_ForNonDirectory()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(file, new Rect(10, 20, 160, 80));

        Assert.Equal(default, headerBounds);
    }

    [Fact]
    public void GetLabelBounds_UsesInsetArea_ForFiles()
    {
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        var labelBounds = TreemapVisualRules.GetLabelBounds(file, new Rect(10, 20, 100, 40));

        Assert.Equal(15, labelBounds.X);
        Assert.Equal(24, labelBounds.Y);
        Assert.Equal(90, labelBounds.Width);
        Assert.Equal(32, labelBounds.Height);
    }

    [Fact]
    public void CanDrawLabel_ReturnsTrue_AtExactThresholds()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);
        var file = CreateNode("file.cs", ProjectNodeKind.File);

        Assert.True(TreemapVisualRules.CanDrawLabel(directory, new Rect(0, 0, 80, 30)));
        Assert.True(TreemapVisualRules.CanDrawLabel(file, new Rect(0, 0, 74, 24)));
    }

    [Fact]
    public void GetHeaderBounds_UsesReducedDirectoryHeaderSlot()
    {
        var directory = CreateNode("src", ProjectNodeKind.Directory);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(directory, new Rect(10, 20, 80, 30));

        Assert.Equal(12, headerBounds.X);
        Assert.Equal(22, headerBounds.Y);
        Assert.Equal(76, headerBounds.Width);
        Assert.Equal(12, headerBounds.Height);
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
                TotalLines: 20,
                FileSizeBytes: 100,
                DescendantFileCount: kind == ProjectNodeKind.File ? 1 : 0,
                DescendantDirectoryCount: kind == ProjectNodeKind.Directory ? 1 : 0),
        };
}

