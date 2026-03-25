using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Infrastructure.Scanning;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class FileSystemProjectScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-scan-{Guid.NewGuid():N}");

    public FileSystemProjectScannerTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ScanAsync_BuildsDeterministicDirectoryTree()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "b-folder"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "a-folder"));
        File.WriteAllText(Path.Combine(_rootPath, "z-file.txt"), "z");
        File.WriteAllText(Path.Combine(_rootPath, "a-file.txt"), "a");
        File.WriteAllText(Path.Combine(_rootPath, "a-folder", "nested.txt"), "nested");

        var scanner = new FileSystemProjectScanner(pathNormalizer: new PathNormalizer());

        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);

        Assert.Equal(_rootPath, snapshot.RootPath);
        Assert.Equal("/", snapshot.Root.Id);
        Assert.Equal(4, snapshot.Root.Children.Count);
        Assert.Collection(
            snapshot.Root.Children,
            node => Assert.Equal("a-folder", node.Name),
            node => Assert.Equal("b-folder", node.Name),
            node => Assert.Equal("a-file.txt", node.Name),
            node => Assert.Equal("z-file.txt", node.Name));
        Assert.Equal("a-folder/nested.txt", snapshot.Root.Children[0].Children[0].RelativePath);
    }

    [Fact]
    public async Task ScanAsync_RespectsPathFilter()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        File.WriteAllText(Path.Combine(_rootPath, "src", "included.cs"), "class Included {}");
        File.WriteAllText(Path.Combine(_rootPath, "src", "ignored.cs"), "class Ignored {}");

        var scanner = new FileSystemProjectScanner(new TestPathFilter(relativePath => relativePath != "src/ignored.cs"));

        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var srcNode = Assert.Single(snapshot.Root.Children);

        Assert.Equal("src", srcNode.RelativePath);
        Assert.Single(srcNode.Children);
        Assert.Equal("included.cs", srcNode.Children[0].Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class TestPathFilter(Func<string, bool> predicate) : IPathFilter
    {
        public bool IsIncluded(string fullPath, string normalizedRelativePath, bool isDirectory) =>
            predicate(normalizedRelativePath);
    }
}
