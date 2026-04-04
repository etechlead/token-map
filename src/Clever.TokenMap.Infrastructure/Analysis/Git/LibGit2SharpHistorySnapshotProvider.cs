using Clever.TokenMap.Core.Analysis.Git;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Paths;
using LibGit2Sharp;

namespace Clever.TokenMap.Infrastructure.Analysis.Git;

public sealed class LibGit2SharpHistorySnapshotProvider : IGitHistorySnapshotProvider
{
    private static readonly StringComparison PathStringComparison =
        PathComparison.UsesCaseInsensitivePaths
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IAppLogger _logger;
    private readonly PathNormalizer _pathNormalizer;

    public LibGit2SharpHistorySnapshotProvider(
        PathNormalizer? pathNormalizer = null,
        IAppLogger? logger = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        _logger = logger ?? NullAppLogger.Instance;
    }

    public ValueTask<GitHistorySnapshot?> TryCreateAsync(
        string analysisRootPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedAnalysisRootPath = _pathNormalizer.NormalizeRootPath(analysisRootPath);
        try
        {
            var discoveredRepositoryPath = Repository.Discover(normalizedAnalysisRootPath);
            if (string.IsNullOrWhiteSpace(discoveredRepositoryPath))
            {
                _logger.LogTrace(
                    "No git repository was found for the analysis root.",
                    eventCode: "analysis.git_repository_not_found",
                    context: AppIssueContext.Create(("RootPath", normalizedAnalysisRootPath)));
                return ValueTask.FromResult<GitHistorySnapshot?>(null);
            }

            using var repository = new Repository(discoveredRepositoryPath);
            var headCommit = repository.Head.Tip;
            if (headCommit is null)
            {
                _logger.LogTrace(
                    "The git repository has no commits, so git history enrichment is skipped.",
                    eventCode: "analysis.git_repository_empty",
                    context: AppIssueContext.Create(("RootPath", normalizedAnalysisRootPath)));
                return ValueTask.FromResult<GitHistorySnapshot?>(null);
            }

            var normalizedRepositoryRootPath = _pathNormalizer.NormalizeRootPath(repository.Info.WorkingDirectory);
            var normalizedAnalysisRootRelativePath = PathNormalizer.NormalizeRelativePath(
                Path.GetRelativePath(normalizedRepositoryRootPath, normalizedAnalysisRootPath));
            var nowUtc = DateTimeOffset.UtcNow;
            var cutoffUtc = nowUtc.AddDays(-90);
            var accumulatorsByAnalysisRelativePath =
                new Dictionary<string, FileHistoryAccumulator>(PathComparison.Comparer);

            foreach (var commit in repository.Commits.QueryBy(new CommitFilter
                     {
                         IncludeReachableFrom = repository.Head,
                         SortBy = CommitSortStrategies.Time,
                     }))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (commit.Committer.When < cutoffUtc)
                {
                    break;
                }

                if (commit.Parents.Count() > 1)
                {
                    continue;
                }

                var oldTree = commit.Parents.FirstOrDefault()?.Tree;
                var patch = repository.Diff.Compare<Patch>(oldTree, commit.Tree);
                if (!patch.Any())
                {
                    continue;
                }

                var churnByPath = new Dictionary<string, int>(PathComparison.Comparer);
                foreach (var entry in patch)
                {
                    if (string.IsNullOrWhiteSpace(entry.Path))
                    {
                        continue;
                    }

                    var normalizedRepositoryRelativePath = PathNormalizer.NormalizeRelativePath(entry.Path);
                    if (!TryMapToAnalysisRelativePath(
                            normalizedRepositoryRelativePath,
                            normalizedAnalysisRootRelativePath,
                            out var analysisRelativePath))
                    {
                        continue;
                    }

                    checked
                    {
                        var churn = entry.IsBinaryComparison
                            ? 0
                            : entry.LinesAdded + entry.LinesDeleted;
                        churnByPath.TryGetValue(analysisRelativePath, out var existingChurn);
                        churnByPath[analysisRelativePath] = existingChurn + churn;
                    }
                }

                if (churnByPath.Count == 0)
                {
                    continue;
                }

                var touchedPaths = churnByPath.Keys.ToArray();
                var normalizedAuthorEmail = NormalizeAuthorEmail(commit.Author.Email);
                foreach (var analysisRelativePath in touchedPaths)
                {
                    if (!accumulatorsByAnalysisRelativePath.TryGetValue(analysisRelativePath, out var accumulator))
                    {
                        accumulator = new FileHistoryAccumulator();
                        accumulatorsByAnalysisRelativePath.Add(analysisRelativePath, accumulator);
                    }

                    var churn = churnByPath[analysisRelativePath];
                    accumulator.AddCommit(churn, normalizedAuthorEmail);
                }

                for (var firstIndex = 0; firstIndex < touchedPaths.Length; firstIndex++)
                {
                    var firstPath = touchedPaths[firstIndex];
                    var firstAccumulator = accumulatorsByAnalysisRelativePath[firstPath];
                    firstAccumulator.AddCochangedPeerCount(touchedPaths.Length - 1);

                    for (var secondIndex = firstIndex + 1; secondIndex < touchedPaths.Length; secondIndex++)
                    {
                        var secondPath = touchedPaths[secondIndex];
                        var secondAccumulator = accumulatorsByAnalysisRelativePath[secondPath];

                        firstAccumulator.AddCochangedPartner(secondPath);
                        secondAccumulator.AddCochangedPartner(firstPath);
                    }
                }
            }

            var fileHistoryByAnalysisRelativePath = accumulatorsByAnalysisRelativePath.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArtifact(),
                PathComparison.Comparer);

            return ValueTask.FromResult<GitHistorySnapshot?>(new GitHistorySnapshot(
                normalizedRepositoryRootPath,
                headCommit.Sha,
                fileHistoryByAnalysisRelativePath,
                nowUtc));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Collecting git history for metrics enrichment failed; continuing without git signals.",
                eventCode: "analysis.git_history_failed",
                context: AppIssueContext.Create(("RootPath", normalizedAnalysisRootPath)));
            return ValueTask.FromResult<GitHistorySnapshot?>(null);
        }
    }

    private static string NormalizeAuthorEmail(string? authorEmail) =>
        string.IsNullOrWhiteSpace(authorEmail)
            ? string.Empty
            : authorEmail.Trim();

    private static bool TryMapToAnalysisRelativePath(
        string repositoryRelativePath,
        string analysisRootRelativePath,
        out string analysisRelativePath)
    {
        analysisRelativePath = string.Empty;

        if (string.IsNullOrEmpty(repositoryRelativePath))
        {
            return false;
        }

        if (string.IsNullOrEmpty(analysisRootRelativePath))
        {
            analysisRelativePath = repositoryRelativePath;
            return true;
        }

        var normalizedPrefix = $"{analysisRootRelativePath}/";
        if (!repositoryRelativePath.StartsWith(normalizedPrefix, PathStringComparison))
        {
            return false;
        }

        analysisRelativePath = repositoryRelativePath[normalizedPrefix.Length..];
        return analysisRelativePath.Length > 0;
    }

    private sealed class FileHistoryAccumulator
    {
        private readonly HashSet<string> _authorEmails = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _cochangePartnerTouchCounts = new(PathComparison.Comparer);

        public int ChurnLines90d { get; private set; }

        public int TouchCount90d { get; private set; }

        public int TotalCochangedPeerCount { get; private set; }

        public void AddCommit(int churnLines, string authorEmail)
        {
            checked
            {
                ChurnLines90d += churnLines;
                TouchCount90d++;
            }

            if (!string.IsNullOrWhiteSpace(authorEmail))
            {
                _authorEmails.Add(authorEmail);
            }
        }

        public void AddCochangedPeerCount(int cochangedPeerCount)
        {
            checked
            {
                TotalCochangedPeerCount += cochangedPeerCount;
            }
        }

        public void AddCochangedPartner(string analysisRelativePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(analysisRelativePath);

            _cochangePartnerTouchCounts.TryGetValue(analysisRelativePath, out var existingCount);
            _cochangePartnerTouchCounts[analysisRelativePath] = existingCount + 1;
        }

        public GitFileHistoryArtifact ToArtifact() =>
            new(
                ChurnLines90d,
                TouchCount90d,
                _authorEmails.Count,
                _cochangePartnerTouchCounts.Count,
                _cochangePartnerTouchCounts.Values.Count(count => count >= 2),
                TouchCount90d == 0
                    ? 0d
                    : (double)TotalCochangedPeerCount / TouchCount90d);
    }
}
