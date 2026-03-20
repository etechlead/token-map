namespace Clever.TokenMap.Core.Models;

public sealed record NodeMetrics(
    long Tokens,
    int TotalLines,
    int? CodeLines,
    int? CommentLines,
    int? BlankLines,
    string? Language,
    long FileSizeBytes,
    int DescendantFileCount,
    int DescendantDirectoryCount)
{
    public static NodeMetrics Empty { get; } = new(
        Tokens: 0,
        TotalLines: 0,
        CodeLines: null,
        CommentLines: null,
        BlankLines: null,
        Language: null,
        FileSizeBytes: 0,
        DescendantFileCount: 0,
        DescendantDirectoryCount: 0);
}
