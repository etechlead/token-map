using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Infrastructure.Analysis;

public sealed class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly IAppLogger _logger;
    private readonly IProjectSnapshotMetricEngine _metricEngine;
    private readonly int _progressBatchSize;
    private readonly IProjectScanner _projectScanner;

    public ProjectAnalyzer(
        IProjectScanner projectScanner,
        IProjectSnapshotMetricEngine metricEngine,
        int progressBatchSize = 64,
        IAppLoggerFactory? loggerFactory = null)
    {
        _projectScanner = projectScanner;
        _metricEngine = metricEngine;
        _progressBatchSize = progressBatchSize;
        var effectiveLoggerFactory = loggerFactory ?? NullAppLoggerFactory.Instance;
        _logger = effectiveLoggerFactory.CreateLogger<ProjectAnalyzer>();
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
            "Analysis started.",
            eventCode: "analysis.started",
            context: AppIssueContext.Create(
                ("RootPath", rootPath),
                ("RespectGitIgnore", options.RespectGitIgnore),
                ("UseGlobalExcludes", options.UseGlobalExcludes),
                ("GlobalExcludeCount", options.GlobalExcludes.Count)));

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

            var enrichedSnapshot = await _metricEngine.EnrichAsync(
                scannedSnapshot,
                bufferedProgress,
                cancellationToken);

            bufferedProgress.Report(new AnalysisProgress(
                Phase: "Completed",
                ProcessedNodeCount: enrichedSnapshot.Root.Summary.DescendantFileCount,
                TotalNodeCount: enrichedSnapshot.Root.Summary.DescendantFileCount,
                CurrentPath: null));
            bufferedProgress.Flush();

            var duration = DateTimeOffset.Now - startedAt;
            var tokenCount = enrichedSnapshot.Root.ComputedMetrics.TryGetRoundedInt64(MetricIds.Tokens) ?? 0;
            var nonEmptyLineCount = enrichedSnapshot.Root.ComputedMetrics.TryGetRoundedInt32(MetricIds.NonEmptyLines) ?? 0;
            _logger.LogInformation(
                "Analysis completed.",
                eventCode: "analysis.completed",
                context: AppIssueContext.Create(
                    ("RootPath", rootPath),
                    ("DurationSeconds", duration.TotalSeconds),
                    ("FileCount", enrichedSnapshot.Root.Summary.DescendantFileCount),
                    ("TokenCount", tokenCount),
                    ("NonEmptyLineCount", nonEmptyLineCount),
                    ("DiagnosticCount", enrichedSnapshot.Diagnostics.Count)));

            return enrichedSnapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            bufferedProgress.Flush();
            var duration = DateTimeOffset.Now - startedAt;
            _logger.LogInformation(
                "Analysis cancelled.",
                eventCode: "analysis.cancelled_core",
                context: AppIssueContext.Create(
                    ("RootPath", rootPath),
                    ("DurationSeconds", duration.TotalSeconds)));
            throw;
        }
        catch (Exception exception)
        {
            bufferedProgress.Flush();
            var duration = DateTimeOffset.Now - startedAt;
            _logger.LogError(
                exception,
                "Analysis failed.",
                eventCode: "analysis.failed_core",
                context: AppIssueContext.Create(
                    ("RootPath", rootPath),
                    ("DurationSeconds", duration.TotalSeconds)));
            throw;
        }
    }
}
