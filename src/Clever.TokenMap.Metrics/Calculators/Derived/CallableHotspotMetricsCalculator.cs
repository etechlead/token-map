using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics.Derived;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class CallableHotspotMetricsCalculator : IFileDerivedMetricCalculator
{
    private readonly CallableHotspotThresholds _thresholds;

    public CallableHotspotMetricsCalculator(CallableHotspotThresholds? thresholds = null)
    {
        _thresholds = thresholds ?? CallableHotspotThresholds.Default;
    }

    public int Order => 150;

    public async ValueTask ComputeAsync(
        IFileMetricContext context,
        MetricSet inputMetrics,
        IMetricSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inputMetrics);
        ArgumentNullException.ThrowIfNull(sink);

        cancellationToken.ThrowIfCancellationRequested();

        var syntaxSummary = await context.GetArtifactAsync<SyntaxSummaryArtifact>(cancellationToken).ConfigureAwait(false);
        if (syntaxSummary is null ||
            syntaxSummary.ParseQuality is SyntaxParseQuality.Unsupported or SyntaxParseQuality.Failed)
        {
            SetAllNotApplicable(sink);
            return;
        }

        var callables = syntaxSummary.Callables;
        if (callables.Count == 0)
        {
            sink.SetValue(MetricIds.MaxCallableLines, 0);
            sink.SetNotApplicable(MetricIds.AverageCallableLines);
            sink.SetValue(MetricIds.LongCallableCount, 0);
            sink.SetValue(MetricIds.HighCyclomaticComplexityCallableCount, 0);
            sink.SetValue(MetricIds.DeepNestingCallableCount, 0);
            sink.SetValue(MetricIds.LongParameterListCount, 0);
            sink.SetValue(MetricIds.CallableHotspotPointsV0, 0);
            return;
        }

        var callableLineCounts = callables
            .Select(callable => callable.Lines.EndLine1Based - callable.Lines.StartLine1Based + 1)
            .ToArray();
        var longCallableCount = callableLineCounts.Count(lineCount => lineCount >= _thresholds.LongCallableLines);
        var highCyclomaticComplexityCallableCount = callables.Count(
            callable => callable.CyclomaticComplexity >= _thresholds.HighCyclomaticComplexity);
        var deepNestingCallableCount = callables.Count(
            callable => callable.MaxNestingDepth >= _thresholds.DeepNestingDepth);
        var longParameterListCount = callables.Count(
            callable => callable.ParameterCount >= _thresholds.LongParameterList);
        var hotspotPoints =
            (2 * longCallableCount) +
            (3 * highCyclomaticComplexityCallableCount) +
            (2 * deepNestingCallableCount) +
            longParameterListCount;

        sink.SetValue(MetricIds.MaxCallableLines, callableLineCounts.Max());
        sink.SetValue(MetricIds.AverageCallableLines, callableLineCounts.Average());
        sink.SetValue(MetricIds.LongCallableCount, longCallableCount);
        sink.SetValue(MetricIds.HighCyclomaticComplexityCallableCount, highCyclomaticComplexityCallableCount);
        sink.SetValue(MetricIds.DeepNestingCallableCount, deepNestingCallableCount);
        sink.SetValue(MetricIds.LongParameterListCount, longParameterListCount);
        sink.SetValue(MetricIds.CallableHotspotPointsV0, hotspotPoints);
    }

    private static void SetAllNotApplicable(IMetricSink sink)
    {
        sink.SetNotApplicable(MetricIds.MaxCallableLines);
        sink.SetNotApplicable(MetricIds.AverageCallableLines);
        sink.SetNotApplicable(MetricIds.LongCallableCount);
        sink.SetNotApplicable(MetricIds.HighCyclomaticComplexityCallableCount);
        sink.SetNotApplicable(MetricIds.DeepNestingCallableCount);
        sink.SetNotApplicable(MetricIds.LongParameterListCount);
        sink.SetNotApplicable(MetricIds.CallableHotspotPointsV0);
    }
}
