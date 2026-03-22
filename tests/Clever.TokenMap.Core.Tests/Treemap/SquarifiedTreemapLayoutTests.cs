using Avalonia;
using Clever.TokenMap.Treemap;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Tests.Treemap;

public sealed class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void Calculate_ReturnsRectsInsideBoundsWithoutOverlap()
    {
        var root = CreateTree();
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 300, 180), AnalysisMetric.Tokens);

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

        var tokenVisuals = layout.Calculate(root, new Rect(0, 0, 240, 120), AnalysisMetric.Tokens);
        var codeVisuals = layout.Calculate(root, new Rect(0, 0, 240, 120), AnalysisMetric.NonEmptyLines);

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

    [Fact]
    public void Calculate_DoesNotDegenerateEqualWeightsIntoFullWidthStripes()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            600,
            60,
            CreateNode("a.cs", ProjectNodeKind.File, 100, 10),
            CreateNode("b.cs", ProjectNodeKind.File, 100, 10),
            CreateNode("c.cs", ProjectNodeKind.File, 100, 10),
            CreateNode("d.cs", ProjectNodeKind.File, 100, 10),
            CreateNode("e.cs", ProjectNodeKind.File, 100, 10),
            CreateNode("f.cs", ProjectNodeKind.File, 100, 10));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 300, 180), AnalysisMetric.Tokens);

        var topLevelVisuals = visuals
            .Where(visual => visual.Depth == 0)
            .ToList();

        Assert.Contains(
            topLevelVisuals,
            visual => visual.Bounds.Width < 270 && visual.Bounds.Height < 162);
    }

    [Fact]
    public void Calculate_LandscapeBounds_StartsWithColumnInsteadOfHorizontalStripe()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            1_068,
            0,
            CreateNode("a", ProjectNodeKind.File, 500, 0),
            CreateNode("b", ProjectNodeKind.File, 433, 0),
            CreateNode("c", ProjectNodeKind.File, 78, 0),
            CreateNode("d", ProjectNodeKind.File, 25, 0),
            CreateNode("e", ProjectNodeKind.File, 25, 0),
            CreateNode("f", ProjectNodeKind.File, 7, 0));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 700, 433), AnalysisMetric.Tokens);

        var first = visuals
            .Where(visual => visual.Depth == 0)
            .OrderByDescending(visual => visual.Node.Metrics.Tokens)
            .First();

        Assert.True(first.Bounds.Height > 400, $"Expected a full-height leading column, got {first.Bounds}.");
        Assert.True(first.Bounds.Width < 433, $"Expected the leading item to be a column, got {first.Bounds}.");
    }

    [Fact]
    public void Calculate_SkewedWeights_UsesBalancedSplitToAvoidExtremeTailStripes()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            1_068,
            0,
            CreateNode("a", ProjectNodeKind.File, 500, 0),
            CreateNode("b", ProjectNodeKind.File, 433, 0),
            CreateNode("c", ProjectNodeKind.File, 78, 0),
            CreateNode("d", ProjectNodeKind.File, 25, 0),
            CreateNode("e", ProjectNodeKind.File, 25, 0),
            CreateNode("f", ProjectNodeKind.File, 7, 0));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 700, 433), AnalysisMetric.Tokens);

        var worstAspectRatio = visuals
            .Where(visual => visual.Depth == 0)
            .Max(visual => GetAspectRatio(visual.Bounds));

        Assert.True(worstAspectRatio < 4.0, $"Expected balanced split fallback to avoid extreme stripe-like bounds, got worst aspect ratio {worstAspectRatio:F3}.");
    }

    [Fact]
    public void Calculate_ThreeSkewedSiblings_DoesNotLeaveSmallestNodeAsFullHeightStripe()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            182,
            0,
            CreateNode("SquarifiedTreemapLayoutTests.cs", ProjectNodeKind.File, 109, 0),
            CreateNode("TreemapVisualRulesTests.cs", ProjectNodeKind.File, 46, 0),
            CreateNode("TreemapColorRulesTests.cs", ProjectNodeKind.File, 27, 0));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 360, 220), AnalysisMetric.Tokens);

        var smallest = visuals.Single(visual => visual.Depth == 0 && visual.Node.RelativePath == "TreemapColorRulesTests.cs");

        Assert.True(smallest.Bounds.Width > 80, $"Expected the smallest sibling to remain visually targetable, got {smallest.Bounds}.");
        Assert.True(smallest.Bounds.Height < 180, $"Expected the smallest sibling to avoid a full-height stripe, got {smallest.Bounds}.");
    }

    [Fact]
    public void Calculate_DirectoryChildren_ArePlacedBelowDirectoryHeaderWhenSpaceAllows()
    {
        var directory = CreateNode(
            "src",
            ProjectNodeKind.Directory,
            100,
            10,
            CreateNode("src/file.cs", ProjectNodeKind.File, 100, 10));
        var root = CreateNode(string.Empty, ProjectNodeKind.Root, 100, 10, directory);
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 300, 180), AnalysisMetric.Tokens);

        var directoryVisual = visuals.Single(visual => visual.Node.RelativePath == "src");
        var fileVisual = visuals.Single(visual => visual.Node.RelativePath == "src/file.cs");

        Assert.True(fileVisual.Bounds.Y >= directoryVisual.Bounds.Y + 14, $"Expected child bounds below directory header, got dir {directoryVisual.Bounds} and file {fileVisual.Bounds}.");
    }

    [Fact]
    public void Calculate_Throws_ForNullRoot()
    {
        var layout = new SquarifiedTreemapLayout();

        Assert.Throws<ArgumentNullException>(() => layout.Calculate(null!, new Rect(0, 0, 300, 180), AnalysisMetric.Tokens));
    }

    [Fact]
    public void Calculate_ReturnsEmpty_ForNonPositiveBounds()
    {
        var root = CreateTree();
        var layout = new SquarifiedTreemapLayout();

        Assert.Empty(layout.Calculate(root, new Rect(0, 0, 0, 180), AnalysisMetric.Tokens));
        Assert.Empty(layout.Calculate(root, new Rect(0, 0, 300, 0), AnalysisMetric.Tokens));
    }

    [Fact]
    public void Calculate_IgnoresZeroWeightChildren()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            100,
            100,
            CreateNode("zero.cs", ProjectNodeKind.File, 0, 0),
            CreateNode("keep.cs", ProjectNodeKind.File, 100, 100));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 300, 180), AnalysisMetric.Tokens);

        Assert.DoesNotContain(visuals, visual => visual.Node.RelativePath == "zero.cs");
        Assert.Contains(visuals, visual => visual.Node.RelativePath == "keep.cs");
    }

    [Fact]
    public void Calculate_PortraitBounds_StartsWithRowInsteadOfVerticalStripe()
    {
        var root = CreateNode(
            string.Empty,
            ProjectNodeKind.Root,
            1_068,
            0,
            CreateNode("a", ProjectNodeKind.File, 500, 0),
            CreateNode("b", ProjectNodeKind.File, 433, 0),
            CreateNode("c", ProjectNodeKind.File, 78, 0),
            CreateNode("d", ProjectNodeKind.File, 25, 0),
            CreateNode("e", ProjectNodeKind.File, 25, 0),
            CreateNode("f", ProjectNodeKind.File, 7, 0));
        var layout = new SquarifiedTreemapLayout();

        var visuals = layout.Calculate(root, new Rect(0, 0, 433, 700), AnalysisMetric.Tokens);

        var first = visuals
            .Where(visual => visual.Depth == 0)
            .OrderByDescending(visual => visual.Node.Metrics.Tokens)
            .First();

        Assert.True(first.Bounds.Width > 400, $"Expected a full-width leading row, got {first.Bounds}.");
        Assert.True(first.Bounds.Height < 433, $"Expected the leading item to be a row, got {first.Bounds}.");
    }

    private static double GetAspectRatio(Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return double.PositiveInfinity;
        }

        return Math.Max(bounds.Width / bounds.Height, bounds.Height / bounds.Width);
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
                NonEmptyLines: codeLines,
                BlankLines: 0,
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

