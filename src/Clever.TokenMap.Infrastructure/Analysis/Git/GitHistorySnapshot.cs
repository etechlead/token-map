using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Analysis.Git;

public sealed class GitHistorySnapshot
{
    private readonly IReadOnlyDictionary<string, GitFileHistoryArtifact> _fileHistoryByAnalysisRelativePath;

    public GitHistorySnapshot(
        string headCommitSha,
        IReadOnlyDictionary<string, GitFileHistoryArtifact> fileHistoryByAnalysisRelativePath,
        DateTimeOffset? historyWindowEndUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headCommitSha);
        ArgumentNullException.ThrowIfNull(fileHistoryByAnalysisRelativePath);

        HeadCommitSha = headCommitSha;
        ContextFingerprint = CreateContextFingerprint(HeadCommitSha, historyWindowEndUtc ?? DateTimeOffset.UtcNow);
        _fileHistoryByAnalysisRelativePath = fileHistoryByAnalysisRelativePath;
    }

    public string HeadCommitSha { get; }

    public string ContextFingerprint { get; }

    public IReadOnlyDictionary<string, GitFileHistoryArtifact> FileHistoryByAnalysisRelativePath =>
        _fileHistoryByAnalysisRelativePath;

    public bool TryGetFileHistory(string analysisRelativePath, out GitFileHistoryArtifact artifact) =>
        _fileHistoryByAnalysisRelativePath.TryGetValue(
            PathNormalizer.NormalizeRelativePath(analysisRelativePath),
            out artifact!);

    private static string CreateContextFingerprint(string headCommitSha, DateTimeOffset historyWindowEndUtc)
    {
        var normalizedWindowEndUtc = historyWindowEndUtc.ToUniversalTime();
        return $"{headCommitSha}|git90d-v1|{normalizedWindowEndUtc:yyyyMMdd}";
    }
}
