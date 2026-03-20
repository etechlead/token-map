using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly ProjectSnapshotMetricsEnricher _metricsEnricher;
    private readonly int _progressBatchSize;
    private readonly IProjectScanner _projectScanner;

    public ProjectAnalyzer(
        IProjectScanner projectScanner,
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ITokeiRunner tokeiRunner,
        ICacheStore? cacheStore = null,
        int progressBatchSize = 64)
    {
        _projectScanner = projectScanner;
        _progressBatchSize = progressBatchSize;
        _metricsEnricher = new ProjectSnapshotMetricsEnricher(
            textFileDetector,
            tokenCounter,
            tokeiRunner,
            cacheStore);
    }

    public async Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);

        var bufferedProgress = new BufferedAnalysisProgress(progress, _progressBatchSize);

        try
        {
            bufferedProgress.Report(new AnalysisProgress(
                Phase: "Initializing",
                ProcessedNodeCount: 0,
                TotalNodeCount: null,
                CurrentPath: null));

            var scannedSnapshot = await _projectScanner.ScanAsync(
                rootPath,
                options,
                bufferedProgress,
                cancellationToken);

            bufferedProgress.Flush();

            var enrichedSnapshot = await _metricsEnricher.EnrichAsync(
                scannedSnapshot,
                bufferedProgress,
                cancellationToken);

            bufferedProgress.Report(new AnalysisProgress(
                Phase: "Completed",
                ProcessedNodeCount: enrichedSnapshot.Root.Metrics.DescendantFileCount,
                TotalNodeCount: enrichedSnapshot.Root.Metrics.DescendantFileCount,
                CurrentPath: null));
            bufferedProgress.Flush();

            return enrichedSnapshot;
        }
        catch
        {
            bufferedProgress.Flush();
            throw;
        }
    }
}
