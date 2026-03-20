using Avalonia;
using Clever.TokenMap.Controls.Layout;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void Calculate_ReturnsRectsInsideBoundsWithoutOverlap()
    {
        var root = CreateTree();
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 300, 180), "Tokens");

        Assert.NotEmpty(visuals);
        Assert.All(visuals, visual =>
        {
            Assert.True(visual.Bounds.Width >= 0);
            Assert.True(visual.Bounds.Height >= 0);
            Assert.True(visual.Bounds.X >= 0);
            Assert.True(visual.Bounds.Y >= 0);
            Assert.True(visual.Bounds.Right <= 300.001);
            Assert.True(visual.Bounds.Bottom <= 180.001);
        });

        var topLevelVisuals = visuals
            .Where(visual => visual.Depth == 0)
            .ToList();

        for (var left = 0; left < topLevelVisuals.Count; left++)
        {
            for (var right = left + 1; right < topLevelVisuals.Count; right++)
            {
                var overlap = topLevelVisuals[left].Bounds.Intersect(topLevelVisuals[right].Bounds);
                Assert.True(overlap.Width < 0.01 || overlap.Height < 0.01);
            }
        }
    }

    [Fact]
    public void Calculate_UsesSelectedMetricForWeighting()
    {
        var root = CreateTree();
        var layout = new SquarifiedTreemapLayout();

        var tokenVisuals = layout.Calculate(root, new Rect(0, 0, 240, 120), "Tokens");
        var codeVisuals = layout.Calculate(root, new Rect(0, 0, 240, 120), "Code lines");

        var tokensLargest = tokenVisuals
            .Where(visual => visual.Node.RelativePath is "src" or "docs")
            .OrderByDescending(visual => visual.Bounds.Width * visual.Bounds.Height)
            .First();
        var codeLargest = codeVisuals
            .Where(visual => visual.Node.RelativePath is "src" or "docs")
            .OrderByDescending(visual => visual.Bounds.Width * visual.Bounds.Height)
            .First();

        Assert.Equal("src", tokensLargest.Node.RelativePath);
        Assert.Equal("docs", codeLargest.Node.RelativePath);
    }

    private static ProjectNode CreateTree()
    {
        var src = CreateNode(
            "src",
            ProjectNodeKind.Directory,
            90,
            10,
            CreateNode("src/app.cs", ProjectNodeKind.File, 60, 5),
            CreateNode("src/lib.cs", ProjectNodeKind.File, 30, 5));
        var docs = CreateNode(
            "docs",
            ProjectNodeKind.Directory,
            30,
            50,
            CreateNode("docs/readme.md", ProjectNodeKind.File, 30, 50));

        var root = CreateNode(string.Empty, ProjectNodeKind.Root, 120, 60, src, docs);
        return root;
    }

    private static ProjectNode CreateNode(
        string relativePath,
        ProjectNodeKind kind,
        long tokens,
        int codeLines,
        params ProjectNode[] children)
    {
        var node = new ProjectNode
        {
            Id = string.IsNullOrEmpty(relativePath) ? "/" : relativePath,
            Name = string.IsNullOrEmpty(relativePath) ? "root" : Path.GetFileName(relativePath),
            FullPath = string.IsNullOrEmpty(relativePath) ? "C:\\root" : $"C:\\root\\{relativePath.Replace('/', '\\')}",
            RelativePath = relativePath,
            Kind = kind,
            Metrics = new NodeMetrics(
                Tokens: tokens,
                TotalLines: codeLines,
                CodeLines: codeLines,
                CommentLines: 0,
                BlankLines: 0,
                Language: null,
                FileSizeBytes: tokens,
                DescendantFileCount: kind == ProjectNodeKind.File ? 1 : children.Sum(child => child.Metrics.DescendantFileCount),
                DescendantDirectoryCount: kind == ProjectNodeKind.File ? 0 : children.Count(child => child.Kind != ProjectNodeKind.File)),
        };

        foreach (var child in children)
        {
            node.Children.Add(child);
        }

        return node;
    }
}
