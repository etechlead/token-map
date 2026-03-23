namespace Clever.TokenMap.Core.Models;

public sealed class ScanOptions
{
    public static ScanOptions Default { get; } = new();

    public bool RespectGitIgnore { get; init; } = true;

    public bool UseDefaultExcludes { get; init; } = true;

    public IReadOnlyList<string> UserExcludes { get; init; } = [];
}
