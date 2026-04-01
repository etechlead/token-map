using Avalonia.Media;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Tests.Support;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.Tests.Treemap;

public sealed class TreemapColorRulesTests
{
    [Fact]
    public void GetParentDirectorySeed_ReturnsRoot_ForTopLevelFile()
    {
        var node = CreateFile("package-lock.json");

        var seed = TreemapColorRules.GetParentDirectorySeed(node);

        Assert.Equal("(root)", seed);
    }

    [Fact]
    public void GetLeafColor_PlainPalette_ReturnsSameColor_ForSiblingsUnderSameParent()
    {
        var first = CreateFile("src/app/a.cs");
        var second = CreateFile("src/app/b.cs");
        var context = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.Tokens);

        var firstColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Plain, context);
        var secondColor = TreemapColorRules.GetLeafColor(second, TreemapPalette.Plain, context);

        Assert.Equal("src/app", TreemapColorRules.GetParentDirectorySeed(first));
        Assert.Equal(firstColor, secondColor);
    }

    [Fact]
    public void GetLeafColor_PlainPalette_ReturnsDifferentColor_ForDifferentParents()
    {
        var first = CreateFile("src/app/a.cs");
        var second = CreateFile("tests/app/a.cs");
        var context = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.Tokens);

        var firstColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Plain, context);
        var secondColor = TreemapColorRules.GetLeafColor(second, TreemapPalette.Plain, context);

        Assert.NotEqual(TreemapColorRules.GetParentDirectorySeed(first), TreemapColorRules.GetParentDirectorySeed(second));
        Assert.NotEqual(firstColor, secondColor);
    }

    [Fact]
    public void GetParentDirectorySeed_NormalizesWindowsSeparators_AndTrimsSlashes()
    {
        var node = CreateFile("\\src\\app\\a.cs\\");

        var seed = TreemapColorRules.GetParentDirectorySeed(node);

        Assert.Equal("src/app", seed);
    }

    [Fact]
    public void GetLeafColor_PlainPalette_ReturnsSameColor_ForEquivalentSlashStyles()
    {
        var first = CreateFile("src/app/a.cs");
        var second = CreateFile("src\\app\\b.cs");
        var context = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.Tokens);

        var firstColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Plain, context);
        var secondColor = TreemapColorRules.GetLeafColor(second, TreemapPalette.Plain, context);

        Assert.Equal(firstColor, secondColor);
    }

    [Fact]
    public void GetLeafColor_WeightedPalette_ReturnsDifferentColor_ForDifferentWeightsUnderSameParent()
    {
        var larger = CreateFile("src/app/a.cs", tokens: 600);
        var smaller = CreateFile("src/app/b.cs", tokens: 40);
        var context = TreemapColorRules.CreatePaletteContext([larger, smaller], MetricIds.Tokens);

        var largerColor = TreemapColorRules.GetLeafColor(larger, TreemapPalette.Weighted, context);
        var smallerColor = TreemapColorRules.GetLeafColor(smaller, TreemapPalette.Weighted, context);

        Assert.Equal(TreemapColorRules.GetParentDirectorySeed(larger), TreemapColorRules.GetParentDirectorySeed(smaller));
        Assert.NotEqual(largerColor, smallerColor);
        Assert.True(GetBrightness(largerColor) > GetBrightness(smallerColor));
    }

    [Fact]
    public void GetLeafColor_WeightedPalette_TracksSelectedMetricWeight()
    {
        var first = CreateFile("src/app/a.cs", tokens: 900, totalLines: 50, fileSizeBytes: 50);
        var second = CreateFile("src/app/b.cs", tokens: 60, totalLines: 600, fileSizeBytes: 600);
        var tokensContext = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.Tokens);
        var linesContext = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.NonEmptyLines);

        var tokensColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Weighted, tokensContext);
        var linesColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Weighted, linesContext);

        Assert.NotEqual(tokensColor, linesColor);
        Assert.True(GetBrightness(tokensColor) > GetBrightness(linesColor));
    }

    [Fact]
    public void GetLeafColor_StudioPalette_ReturnsDifferentColor_ForDifferentWeightsUnderSameParent()
    {
        var larger = CreateFile("src/app/a.cs", tokens: 800);
        var smaller = CreateFile("src/app/b.cs", tokens: 30);
        var context = TreemapColorRules.CreatePaletteContext([larger, smaller], MetricIds.Tokens);

        var largerColor = TreemapColorRules.GetLeafColor(larger, TreemapPalette.Studio, context);
        var smallerColor = TreemapColorRules.GetLeafColor(smaller, TreemapPalette.Studio, context);

        Assert.NotEqual(largerColor, smallerColor);
        Assert.True(GetBrightness(largerColor) > GetBrightness(smallerColor));
    }

    [Fact]
    public void GetLeafColor_StudioPalette_ReturnsDifferentFamilies_ForDifferentParents()
    {
        var first = CreateFile("src/app/a.cs", tokens: 200);
        var second = CreateFile("tests/app/a.cs", tokens: 200);
        var context = TreemapColorRules.CreatePaletteContext([first, second], MetricIds.Tokens);

        var firstColor = TreemapColorRules.GetLeafColor(first, TreemapPalette.Studio, context);
        var secondColor = TreemapColorRules.GetLeafColor(second, TreemapPalette.Studio, context);

        Assert.NotEqual(firstColor, secondColor);
    }

    [Fact]
    public void GetLeafColor_StudioPalette_ProducesBroadColorVariety_ForManyParents()
    {
        var nodes = Enumerable.Range(0, 12)
            .Select(index => CreateFile($"group-{index}/item.cs", tokens: 200))
            .ToArray();
        var context = TreemapColorRules.CreatePaletteContext(nodes, MetricIds.Tokens);

        var distinctColors = nodes
            .Select(node => TreemapColorRules.GetLeafColor(node, TreemapPalette.Studio, context))
            .Distinct()
            .Count();

        Assert.True(distinctColors >= 10, $"Expected at least 10 distinct Studio colors, got {distinctColors}.");
    }

    [Fact]
    public void CreatePaletteContext_UsesVisibleLeafRange()
    {
        var first = CreateFile("a.cs", tokens: 10);
        var second = CreateFile("b.cs", tokens: 100);
        var third = CreateFile("c.cs", tokens: 1_000);

        var context = TreemapColorRules.CreatePaletteContext([first, second, third], MetricIds.Tokens);

        Assert.Equal(MetricIds.Tokens, context.Metric);
        Assert.Equal(10, context.MinLeafWeight);
        Assert.Equal(1_000, context.MaxLeafWeight);
    }

    private static double GetBrightness(Color color) =>
        (color.R * 299d) + (color.G * 587d) + (color.B * 114d);

    private static ProjectNode CreateFile(
        string relativePath,
        long tokens = 100,
        int totalLines = 20,
        long fileSizeBytes = 100) =>
        new()
        {
            Id = relativePath,
            Name = Path.GetFileName(relativePath),
            FullPath = $"C:\\root\\{relativePath.Replace('/', '\\')}",
            RelativePath = relativePath,
            Kind = ProjectNodeKind.File,
            Summary = MetricTestData.CreateFileSummary(),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(
                tokens: tokens,
                nonEmptyLines: totalLines,
                fileSizeBytes: fileSizeBytes),
        };
}
