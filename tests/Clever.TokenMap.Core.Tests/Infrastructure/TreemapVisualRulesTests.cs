using Avalonia;
using Clever.TokenMap.Controls;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

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

        Assert.Equal(14, labelBounds.X);
        Assert.Equal(22, labelBounds.Y);
        Assert.Equal(152, labelBounds.Width);
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
                NonEmptyLines: 20,
                BlankLines: 0,
                FileSizeBytes: 100,
                DescendantFileCount: kind == ProjectNodeKind.File ? 1 : 0,
                DescendantDirectoryCount: kind == ProjectNodeKind.Directory ? 1 : 0),
        };
}
