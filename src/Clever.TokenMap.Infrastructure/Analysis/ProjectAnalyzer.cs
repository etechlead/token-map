using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Logging;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly IAppLogger _logger;
    private readonly ProjectSnapshotMetricsEnricher _metricsEnricher;
    private readonly int _progressBatchSize;
    private readonly IProjectScanner _projectScanner;

    public ProjectAnalyzer(
        IProjectScanner projectScanner,
        ITextFileDetector textFileDetector,
        ITokenCounter tokenCounter,
        ITokeiRunner tokeiRunner,
        ICacheStore? cacheStore = null,
        int progressBatchSize = 64,
        IAppLoggerFactory? loggerFactory = null)
    {
        _projectScanner = projectScanner;
        _progressBatchSize = progressBatchSize;
        var effectiveLoggerFactory = loggerFactory ?? NullAppLoggerFactory.Instance;
        _logger = effectiveLoggerFactory.CreateLogger<ProjectAnalyzer>();
        _metricsEnricher = new ProjectSnapshotMetricsEnricher(
            textFileDetector,
            tokenCounter,
            tokeiRunner,
            cacheStore,
            effectiveLoggerFactory.CreateLogger<ProjectSnapshotMetricsEnricher>());
    }

    public async Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTimeOffset.Now;
        _logger.LogInformation(
            $"Analysis started for '{rootPath}' with tokenProfile={options.TokenProfile}, respectGitIgnore={options.RespectGitIgnore}, respectIgnore={options.RespectDotIgnore}, useDefaultExcludes={options.UseDefaultExcludes}.");

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

            var duration = DateTimeOffset.Now - startedAt;
            _logger.LogInformation(
                $"Analysis completed for '{rootPath}' in {duration.TotalSeconds:F2}s. Files={enrichedSnapshot.Root.Metrics.DescendantFileCount:N0}, tokens={enrichedSnapshot.Root.Metrics.Tokens:N0}, lines={enrichedSnapshot.Root.Metrics.TotalLines:N0}, warnings={enrichedSnapshot.Warnings.Count:N0}.");

            return enrichedSnapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            bufferedProgress.Flush();
            var duration = DateTimeOffset.Now - startedAt;
            _logger.LogInformation($"Analysis cancelled for '{rootPath}' after {duration.TotalSeconds:F2}s.");
            throw;
        }
        catch (Exception exception)
        {
            bufferedProgress.Flush();
            var duration = DateTimeOffset.Now - startedAt;
            _logger.LogError(exception, $"Analysis failed for '{rootPath}' after {duration.TotalSeconds:F2}s.");
            throw;
        }
    }
}
