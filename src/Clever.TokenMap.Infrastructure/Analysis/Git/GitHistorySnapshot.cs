using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Analysis.Git;

public sealed class GitHistorySnapshot
{
    private readonly IReadOnlyDictionary<string, GitFileHistoryArtifact> _fileHistoryByAnalysisRelativePath;

    public GitHistorySnapshot(
        string repositoryRootPath,
        string headCommitSha,
        IReadOnlyDictionary<string, GitFileHistoryArtifact> fileHistoryByAnalysisRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(headCommitSha);
        ArgumentNullException.ThrowIfNull(fileHistoryByAnalysisRelativePath);

        RepositoryRootPath = repositoryRootPath;
        HeadCommitSha = headCommitSha;
        ContextFingerprint = $"{HeadCommitSha}|git90d-v0";
        _fileHistoryByAnalysisRelativePath = fileHistoryByAnalysisRelativePath;
    }

    public string RepositoryRootPath { get; }

    public string HeadCommitSha { get; }

    public string ContextFingerprint { get; }

    public IReadOnlyDictionary<string, GitFileHistoryArtifact> FileHistoryByAnalysisRelativePath =>
        _fileHistoryByAnalysisRelativePath;

    public bool TryGetFileHistory(string analysisRelativePath, out GitFileHistoryArtifact artifact) =>
        _fileHistoryByAnalysisRelativePath.TryGetValue(
            PathNormalizer.NormalizeRelativePath(analysisRelativePath),
            out artifact!);
}
