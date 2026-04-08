using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class ComplexityPointsDerivedMetricsCalculator : IFileDerivedMetricCalculator
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

        if (!ProductMetricFormulas.TryComputeStructuralRisk(inputMetrics, out var breakdown))
        {
            sink.SetNotApplicable(MetricIds.ComplexityPoints);
            return ValueTask.CompletedTask;
        }

        sink.SetValue(MetricIds.ComplexityPoints, breakdown.TotalPoints);
        return ValueTask.CompletedTask;
    }
}
