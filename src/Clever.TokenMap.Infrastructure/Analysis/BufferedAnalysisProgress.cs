using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Analysis;

internal sealed class BufferedAnalysisProgress : IProgress<AnalysisProgress>
{
    private readonly int _batchSize;
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

        if (_pendingProgress is not null &&
            !string.Equals(_pendingProgress.Phase, value.Phase, StringComparison.Ordinal))
        {
            Flush();
        }

        _pendingProgress = value;
        _pendingCount++;

        var isTerminal = value.TotalNodeCount.HasValue && value.ProcessedNodeCount >= value.TotalNodeCount.Value;
        if (_pendingCount >= _batchSize || isTerminal)
        {
            Flush();
        }
    }

    public void Flush()
    {
        if (_innerProgress is null || _pendingProgress is null)
        {
            return;
        }

        _innerProgress.Report(_pendingProgress);
        _pendingProgress = null;
        _pendingCount = 0;
    }
}
