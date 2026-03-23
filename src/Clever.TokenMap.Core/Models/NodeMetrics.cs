namespace Clever.TokenMap.Core.Models;

public sealed record NodeMetrics(
    long Tokens,
    int TotalLines,
    long FileSizeBytes,
    int DescendantFileCount,
    int DescendantDirectoryCount)
{
    public static NodeMetrics Empty { get; } = new(
        Tokens: 0,
        TotalLines: 0,
        FileSizeBytes: 0,
        DescendantFileCount: 0,
        DescendantDirectoryCount: 0);
}
