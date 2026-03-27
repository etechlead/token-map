using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Analysis;

internal sealed class BufferedAnalysisProgress : IProgress<AnalysisProgress>
{
    private readonly int _batchSize;
    private readonly object _gate = new();
    private readonly IProgress<AnalysisProgress>? _innerProgress;
    private AnalysisProgress? _pendingProgress;
    private int _pendingCount;

    public BufferedAnalysisProgress(IProgress<AnalysisProgress>? innerProgress, int batchSize = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        _innerProgress = innerProgress;
        _batchSize = batchSize;
    }

    public void Report(AnalysisProgress value)
    {
        if (_innerProgress is null)
        {
            return;
        }

        AnalysisProgress? progressToReport = null;
        lock (_gate)
        {
            if (_pendingProgress is not null &&
                !string.Equals(_pendingProgress.Phase, value.Phase, StringComparison.Ordinal))
            {
                progressToReport = TakePendingProgress();
            }

            _pendingProgress = value;
            _pendingCount++;

            var isTerminal = value.TotalNodeCount.HasValue && value.ProcessedNodeCount >= value.TotalNodeCount.Value;
            if (_pendingCount >= _batchSize || isTerminal)
            {
                progressToReport = TakePendingProgress();
            }
        }

        if (progressToReport is not null)
        {
            _innerProgress.Report(progressToReport);
        }
    }

    public void Flush()
    {
        if (_innerProgress is null || _pendingProgress is null)
        {
            return;
        }

        AnalysisProgress? progressToReport;
        lock (_gate)
        {
            progressToReport = TakePendingProgress();
        }

        if (progressToReport is not null)
        {
            _innerProgress.Report(progressToReport);
        }
    }

    private AnalysisProgress? TakePendingProgress()
    {
        var progressToReport = _pendingProgress;
        _pendingProgress = null;
        _pendingCount = 0;
        return progressToReport;
    }
}
