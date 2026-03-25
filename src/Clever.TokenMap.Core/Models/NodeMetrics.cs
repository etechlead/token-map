namespace Clever.TokenMap.Core.Models;

public sealed record NodeMetrics(
    long Tokens,
    int NonEmptyLines,
    long FileSizeBytes,
    int DescendantFileCount,
    int DescendantDirectoryCount)
{
    public static NodeMetrics Empty { get; } = new(
        Tokens: 0,
        NonEmptyLines: 0,
        FileSizeBytes: 0,
        DescendantFileCount: 0,
        DescendantDirectoryCount: 0);
}
