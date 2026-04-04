namespace Clever.TokenMap.Core.Analysis.Git;

public sealed record GitFileHistoryArtifact(
    int ChurnLines90d,
    int TouchCount90d,
    int AuthorCount90d);
