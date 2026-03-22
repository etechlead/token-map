using Clever.TokenMap.Controls;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

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
    public void GetLeafColor_ReturnsSameColor_ForSiblingsUnderSameParent()
    {
        var first = CreateFile("src/app/a.cs");
        var second = CreateFile("src/app/b.cs");

        var firstColor = TreemapColorRules.GetLeafColor(first);
        var secondColor = TreemapColorRules.GetLeafColor(second);

        Assert.Equal("src/app", TreemapColorRules.GetParentDirectorySeed(first));
        Assert.Equal(firstColor, secondColor);
    }

    [Fact]
    public void GetLeafColor_ReturnsDifferentColor_ForDifferentParents()
    {
        var first = CreateFile("src/app/a.cs");
        var second = CreateFile("tests/app/a.cs");

        var firstColor = TreemapColorRules.GetLeafColor(first);
        var secondColor = TreemapColorRules.GetLeafColor(second);

        Assert.NotEqual(TreemapColorRules.GetParentDirectorySeed(first), TreemapColorRules.GetParentDirectorySeed(second));
        Assert.NotEqual(firstColor, secondColor);
    }

    private static ProjectNode CreateFile(string relativePath) =>
        new()
        {
            Id = relativePath,
            Name = Path.GetFileName(relativePath),
            FullPath = $"C:\\root\\{relativePath.Replace('/', '\\')}",
            RelativePath = relativePath,
            Kind = ProjectNodeKind.File,
            Metrics = new NodeMetrics(
                Tokens: 100,
                TotalLines: 20,
                NonEmptyLines: 20,
                BlankLines: 0,
                FileSizeBytes: 100,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
}
