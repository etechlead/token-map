using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics.Calculators;

public sealed class SyntaxMetricsCalculator : IFileMetricCalculator
{
    public int Order => 200;

    public async ValueTask ComputeAsync(
        IFileMetricContext context,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        var syntaxSummary = await context.GetArtifactAsync<SyntaxSummaryArtifact>(cancellationToken).ConfigureAwait(false);
        if (syntaxSummary is null ||
            syntaxSummary.ParseQuality is SyntaxParseQuality.Unsupported or SyntaxParseQuality.Failed)
        {
            sink.SetNotApplicable(MetricIds.CommentLines);
            sink.SetNotApplicable(MetricIds.FunctionCount);
            sink.SetNotApplicable(MetricIds.CyclomaticComplexitySum);
            sink.SetNotApplicable(MetricIds.CyclomaticComplexityMax);
            sink.SetNotApplicable(MetricIds.MaxNestingDepth);
            return;
        }

        sink.SetValue(MetricIds.CommentLines, syntaxSummary.CommentLineCount);
        sink.SetValue(MetricIds.FunctionCount, syntaxSummary.FunctionCount);
        sink.SetValue(MetricIds.CyclomaticComplexitySum, syntaxSummary.CyclomaticComplexitySum);
        sink.SetValue(MetricIds.CyclomaticComplexityMax, syntaxSummary.CyclomaticComplexityMax);
        sink.SetValue(MetricIds.MaxNestingDepth, syntaxSummary.MaxNestingDepth);
    }
}
