using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class RatioDerivedMetricsCalculator : IFileDerivedMetricCalculator
{
    public int Order => 100;

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

        SetRatio(
            inputMetrics,
            sink,
            MetricIds.AverageParametersPerCallable,
            MetricIds.TotalParameterCount,
            MetricIds.FunctionCount);
        SetRatio(
            inputMetrics,
            sink,
            MetricIds.AverageCyclomaticComplexityPerCallable,
            MetricIds.CyclomaticComplexitySum,
            MetricIds.FunctionCount);
        SetRatio(
            inputMetrics,
            sink,
            MetricIds.CyclomaticComplexityPerCodeLine,
            MetricIds.CyclomaticComplexitySum,
            MetricIds.CodeLines);
        SetCommentRatio(inputMetrics, sink);

        return ValueTask.CompletedTask;
    }

    private static void SetRatio(
        MetricSet inputMetrics,
        IMetricSink sink,
        MetricId targetMetricId,
        MetricId numeratorMetricId,
        MetricId denominatorMetricId)
    {
        var numerator = inputMetrics.TryGetNumber(numeratorMetricId);
        var denominator = inputMetrics.TryGetNumber(denominatorMetricId);

        if (!numerator.HasValue || !denominator.HasValue || denominator.Value <= 0d)
        {
            sink.SetNotApplicable(targetMetricId);
            return;
        }

        sink.SetValue(targetMetricId, numerator.Value / denominator.Value);
    }

    private static void SetCommentRatio(MetricSet inputMetrics, IMetricSink sink)
    {
        var commentLines = inputMetrics.TryGetNumber(MetricIds.CommentLines);
        var codeLines = inputMetrics.TryGetNumber(MetricIds.CodeLines);

        if (!commentLines.HasValue || !codeLines.HasValue)
        {
            sink.SetNotApplicable(MetricIds.CommentRatio);
            return;
        }

        var totalLines = commentLines.Value + codeLines.Value;
        if (totalLines <= 0d)
        {
            sink.SetNotApplicable(MetricIds.CommentRatio);
            return;
        }

        sink.SetValue(MetricIds.CommentRatio, commentLines.Value / totalLines);
    }
}
