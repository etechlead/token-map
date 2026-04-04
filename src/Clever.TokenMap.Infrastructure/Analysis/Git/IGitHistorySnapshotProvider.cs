namespace Clever.TokenMap.Infrastructure.Analysis.Git;

public interface IGitHistorySnapshotProvider
{
    ValueTask<GitHistorySnapshot?> TryCreateAsync(
        string analysisRootPath,
        CancellationToken cancellationToken);
}
