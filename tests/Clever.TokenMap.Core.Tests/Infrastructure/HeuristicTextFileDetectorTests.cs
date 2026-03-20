using Clever.TokenMap.Infrastructure.Text;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class HeuristicTextFileDetectorTests : IDisposable
{
    private readonly HeuristicTextFileDetector _detector = new();
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-text-detector-{Guid.NewGuid():N}");

    public HeuristicTextFileDetectorTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task IsTextAsync_ReturnsTrueForUtf8Text()
    {
        var filePath = Path.Combine(_rootPath, "README.md");
        await File.WriteAllTextAsync(filePath, "hello\nworld\n");

        var isText = await _detector.IsTextAsync(filePath, CancellationToken.None);

        Assert.True(isText);
    }

    [Fact]
    public async Task IsTextAsync_ReturnsFalseForBinaryContent()
    {
        var filePath = Path.Combine(_rootPath, "image.bin");
        await File.WriteAllBytesAsync(filePath, [0x42, 0x00, 0x43, 0x44]);

        var isText = await _detector.IsTextAsync(filePath, CancellationToken.None);

        Assert.False(isText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
