namespace Clever.TokenMap.Core.Models;

public sealed class ScanOptions
{
    public static ScanOptions Default { get; } = new();

    public bool RespectGitIgnore { get; init; } = true;

    public bool UseGlobalExcludes { get; init; } = true;

    public IReadOnlyList<string> GlobalExcludes { get; init; } = GlobalExcludeDefaults.DefaultEntries;
}
