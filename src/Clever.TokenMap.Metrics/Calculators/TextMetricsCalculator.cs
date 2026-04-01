using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators;

public sealed class TextMetricsCalculator : IFileMetricCalculator
{
    public int Order => 100;

    public async ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        var textMetrics = await context.GetArtifactAsync<TextMetricsArtifact>(cancellationToken);
        if (textMetrics is null)
        {
            sink.SetNotApplicable(MetricIds.Tokens);
            sink.SetNotApplicable(MetricIds.NonEmptyLines);
            return;
        }

        sink.SetValue(MetricIds.Tokens, textMetrics.TokenCount);
        sink.SetValue(MetricIds.NonEmptyLines, textMetrics.NonEmptyLineCount);
    }
}
