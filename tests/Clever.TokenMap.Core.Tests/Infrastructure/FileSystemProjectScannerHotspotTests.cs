using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Paths;
using Clever.TokenMap.Infrastructure.Scanning;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class FileSystemProjectScannerHotspotTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-scan-hotspot-{Guid.NewGuid():N}");

    public FileSystemProjectScannerHotspotTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressForRootAndChildrenInTraversalOrder()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        File.WriteAllText(Path.Combine(_rootPath, "src", "nested.txt"), "nested");
        File.WriteAllText(Path.Combine(_rootPath, "root.txt"), "root");

        var progress = new CapturingProgress();
        var scanner = new FileSystemProjectScanner(pathNormalizer: new PathNormalizer());

        var snapshot = await scanner.ScanAsync(_rootPath, ScanOptions.Default, progress, CancellationToken.None);
        var events = progress.GetSnapshot();

        Assert.Equal(_rootPath, snapshot.RootPath);
        Assert.Equal(4, events.Count);
        Assert.Collection(
            events,
            value =>
            {
                Assert.Equal("ScanningTree", value.Phase);
                Assert.Equal(1, value.ProcessedNodeCount);
                Assert.Equal(string.Empty, value.CurrentPath);
            },
            value =>
            {
                Assert.Equal("ScanningTree", value.Phase);
                Assert.Equal(2, value.ProcessedNodeCount);
                Assert.Equal("src", value.CurrentPath);
            },
            value =>
            {
                Assert.Equal("ScanningTree", value.Phase);
                Assert.Equal(3, value.ProcessedNodeCount);
                Assert.Equal("src/nested.txt", value.CurrentPath);
            },
            value =>
            {
                Assert.Equal("ScanningTree", value.Phase);
                Assert.Equal(4, value.ProcessedNodeCount);
                Assert.Equal("root.txt", value.CurrentPath);
            });
    }

    [Fact]
    public async Task ScanAsync_ThrowsForMissingRootPath()
    {
        var missingRootPath = Path.Combine(_rootPath, "missing");
        var scanner = new FileSystemProjectScanner(pathNormalizer: new PathNormalizer());

        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await scanner.ScanAsync(missingRootPath, ScanOptions.Default, progress: null, CancellationToken.None));

        Assert.Contains(missingRootPath, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class CapturingProgress : IProgress<AnalysisProgress>
    {
        private readonly object _gate = new();
        private readonly List<AnalysisProgress> _events = [];

        public void Report(AnalysisProgress value)
        {
            lock (_gate)
            {
                _events.Add(value);
            }
        }

        public IReadOnlyList<AnalysisProgress> GetSnapshot()
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }
}
