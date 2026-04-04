namespace Clever.TokenMap.Core.Analysis.Git;

public sealed record GitFileHistoryArtifact(
    int ChurnLines90d,
    int TouchCount90d,
    int AuthorCount90d,
    int UniqueCochangedFileCount90d,
    int StrongCochangedFileCount90d,
    double AverageCochangeSetSize90d)
{
    public static GitFileHistoryArtifact Zero { get; } = new(
        ChurnLines90d: 0,
        TouchCount90d: 0,
        AuthorCount90d: 0,
        UniqueCochangedFileCount90d: 0,
        StrongCochangedFileCount90d: 0,
        AverageCochangeSetSize90d: 0d);
}
