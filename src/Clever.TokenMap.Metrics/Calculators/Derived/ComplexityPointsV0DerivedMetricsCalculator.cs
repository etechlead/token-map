using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class ComplexityPointsV0DerivedMetricsCalculator : IFileDerivedMetricCalculator
{
    public int Order => 200;

    public ValueTask ComputeAsync(
        IFileMetricContext context,
        MetricSet inputMetrics,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inputMetrics);
        ArgumentNullException.ThrowIfNull(sink);

        cancellationToken.ThrowIfCancellationRequested();

        var codeLines = inputMetrics.TryGetNumber(MetricIds.CodeLines);
        var cyclomaticComplexitySum = inputMetrics.TryGetNumber(MetricIds.CyclomaticComplexitySum);
        var cyclomaticComplexityMax = inputMetrics.TryGetNumber(MetricIds.CyclomaticComplexityMax);
        var maxNestingDepth = inputMetrics.TryGetNumber(MetricIds.MaxNestingDepth);
        var maxParameterCount = inputMetrics.TryGetNumber(MetricIds.MaxParameterCount);

        if (!codeLines.HasValue ||
            !cyclomaticComplexitySum.HasValue ||
            !cyclomaticComplexityMax.HasValue ||
            !maxNestingDepth.HasValue ||
            !maxParameterCount.HasValue)
        {
            sink.SetNotApplicable(MetricIds.ComplexityPointsV0);
            return ValueTask.CompletedTask;
        }

        var score =
            100d * (
                (0.20d * Normalize(codeLines.Value, good: 20d, bad: 300d)) +
                (0.35d * Normalize(cyclomaticComplexitySum.Value, good: 2d, bad: 40d)) +
                (0.20d * Normalize(cyclomaticComplexityMax.Value, good: 2d, bad: 15d)) +
                (0.15d * Normalize(maxNestingDepth.Value, good: 1d, bad: 6d)) +
                (0.10d * Normalize(maxParameterCount.Value, good: 2d, bad: 8d)));

        sink.SetValue(MetricIds.ComplexityPointsV0, score);
        return ValueTask.CompletedTask;
    }

    private static double Normalize(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        return Math.Clamp((value - good) / (bad - good), 0d, 1d);
    }
}
