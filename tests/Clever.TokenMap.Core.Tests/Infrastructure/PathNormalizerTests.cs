using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class PathNormalizerTests
{
    private readonly PathNormalizer _pathNormalizer = new();

    [Fact]
    public void NormalizeRootPath_TrimsTrailingDirectorySeparator()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"tokenmap-root-{Guid.NewGuid():N}");
        var pathWithTrailingSeparator = tempRoot + Path.DirectorySeparatorChar;

        var normalized = _pathNormalizer.NormalizeRootPath(pathWithTrailingSeparator);

        Assert.Equal(tempRoot, normalized);
    }

    [Fact]
    public void NormalizeRelativePath_UsesForwardSlashes()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-root-{Guid.NewGuid():N}");
        var nestedPath = Path.Combine(rootPath, "src", "app", "Program.cs");

        var relativePath = _pathNormalizer.NormalizeRelativePath(rootPath, nestedPath);

        Assert.Equal("src/app/Program.cs", relativePath);
    }

    [Fact]
    public void GetNodeId_ReturnsSlashForRoot()
    {
        Assert.Equal("/", _pathNormalizer.GetNodeId(string.Empty));
    }
}
