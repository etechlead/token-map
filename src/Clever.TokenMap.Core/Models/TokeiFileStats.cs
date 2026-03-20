namespace Clever.TokenMap.Core.Models;

public sealed class TokeiFileStats
{
    public required string RelativePath { get; init; }

    public required int TotalLines { get; init; }

    public int? CodeLines { get; init; }

    public int? CommentLines { get; init; }

    public int? BlankLines { get; init; }

    public string? Language { get; init; }
}
