namespace Clever.TokenMap.Core.Models;

public sealed record NodeSummary(
    int DescendantFileCount,
    int DescendantDirectoryCount)
{
    public static NodeSummary Empty { get; } = new(
        DescendantFileCount: 0,
        DescendantDirectoryCount: 0);
}
