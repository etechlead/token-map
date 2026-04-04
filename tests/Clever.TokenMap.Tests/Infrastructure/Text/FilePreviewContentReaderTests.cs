using System.Text;
using Clever.TokenMap.Core.Preview;
using Clever.TokenMap.Infrastructure.Text;

namespace Clever.TokenMap.Tests.Infrastructure.Text;

public sealed class FilePreviewContentReaderTests : IDisposable
{
    private readonly string _workspacePath = Path.Combine(
        Path.GetTempPath(),
        "tokenmap-file-preview-tests",
        Guid.NewGuid().ToString("N"));

    public FilePreviewContentReaderTests()
    {
        Directory.CreateDirectory(_workspacePath);
    }

    [Fact]
    public async Task ReadAsync_ReturnsSuccess_ForSmallTextFile()
    {
        var filePath = Path.Combine(_workspacePath, "Program.cs");
        await File.WriteAllTextAsync(filePath, "class Program { }", Encoding.UTF8);
        var reader = new FilePreviewContentReader(new HeuristicTextFileDetector());

        var result = await reader.ReadAsync(filePath);

        Assert.Equal(FilePreviewReadStatus.Success, result.Status);
        Assert.Equal("class Program { }", result.Content);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNotText_ForBinaryFile()
    {
        var filePath = Path.Combine(_workspacePath, "image.bin");
        await File.WriteAllBytesAsync(filePath, [0, 159, 146, 150, 0, 0, 0, 0]);
        var reader = new FilePreviewContentReader(new HeuristicTextFileDetector());

        var result = await reader.ReadAsync(filePath);

        Assert.Equal(FilePreviewReadStatus.NotText, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_ReturnsTooLarge_ForFilesBeyondLimit()
    {
        var filePath = Path.Combine(_workspacePath, "large.txt");
        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(FilePreviewContentReader.MaxPreviewFileSizeBytes + 1);
        }

        var reader = new FilePreviewContentReader(new HeuristicTextFileDetector());

        var result = await reader.ReadAsync(filePath);

        Assert.Equal(FilePreviewReadStatus.TooLarge, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_ReturnsMissing_WhenFileDoesNotExist()
    {
        var filePath = Path.Combine(_workspacePath, "missing.txt");
        var reader = new FilePreviewContentReader(new HeuristicTextFileDetector());

        var result = await reader.ReadAsync(filePath);

        Assert.Equal(FilePreviewReadStatus.Missing, result.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacePath))
        {
            Directory.Delete(_workspacePath, recursive: true);
        }
    }
}
