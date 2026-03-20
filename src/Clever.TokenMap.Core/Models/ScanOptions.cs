using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Models;

public sealed class ScanOptions
{
    public static ScanOptions Default { get; } = new();

    public TokenProfile TokenProfile { get; init; } = TokenProfile.O200KBase;

    public bool RespectGitIgnore { get; init; } = true;

    public bool RespectDotIgnore { get; init; } = true;

    public bool UseDefaultExcludes { get; init; } = true;

    public IReadOnlyList<string> UserExcludes { get; init; } = [];
}
