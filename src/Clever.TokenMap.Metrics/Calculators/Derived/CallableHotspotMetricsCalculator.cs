using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Metrics.Formulas;
using Clever.TokenMap.Metrics.Derived;

namespace Clever.TokenMap.Metrics.Calculators.Derived;

public sealed class CallableHotspotMetricsCalculator : IFileDerivedMetricCalculator
{
    private const double LineExcessWeight = 1d;
    private const double MinimumLineComplexityMultiplier = 0.35d;
    private const double CyclomaticComplexityExcessWeight = 4d;
    private const double NestingDepthExcessWeight = 6d;
    private const double ParameterCountExcessWeight = 2.5d;
    private const int SoftCallableLines = 20;
    private const int SoftCyclomaticComplexity = 5;
    private const int SoftNestingDepth = 3;
    private const int SoftParameterCount = 4;

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
            sink.SetValue(MetricIds.LongCallableCount, 0);
            sink.SetValue(MetricIds.HighCyclomaticComplexityCallableCount, 0);
            sink.SetValue(MetricIds.DeepNestingCallableCount, 0);
            sink.SetValue(MetricIds.LongParameterListCount, 0);
            sink.SetValue(MetricIds.CallableHotspotPoints, 0);
            sink.SetValue(MetricIds.CallableCount, 0);
            sink.SetValue(MetricIds.AffectedCallableCount, 0);
            sink.SetValue(MetricIds.AffectedCallableRatio, 0d);
            sink.SetValue(MetricIds.TotalCallableBurdenPoints, 0d);
            sink.SetValue(MetricIds.TopCallableBurdenPoints, 0d);
            sink.SetValue(MetricIds.TopThreeCallableBurdenShare, 0d);
            return;
        }

        var callableLineCounts = callables
            .Select(callable => callable.Lines.EndLine1Based - callable.Lines.StartLine1Based + 1)
            .ToArray();
        var callableBurdenPoints = callables
            .Select(static callable => ComputeCallableBurden(callable))
            .OrderByDescending(points => points)
            .ToArray();
        var longCallableCount = callableLineCounts.Count(lineCount => lineCount >= _thresholds.LongCallableLines);
        var highCyclomaticComplexityCallableCount = callables.Count(
            callable => callable.CyclomaticComplexity >= _thresholds.HighCyclomaticComplexity);
        var deepNestingCallableCount = callables.Count(
            callable => callable.MaxNestingDepth >= _thresholds.DeepNestingDepth);
        var longParameterListCount = callables.Count(
            callable => callable.ParameterCount >= _thresholds.LongParameterList);
        var affectedCallableCount = callableBurdenPoints.Count(points => points > 0d);
        var totalCallableBurdenPoints = callableBurdenPoints.Sum();
        var topCallableBurdenPoints = callableBurdenPoints[0];
        var topThreeCallableBurdenShare = totalCallableBurdenPoints <= 0d
            ? 0d
            : callableBurdenPoints.Take(3).Sum() / totalCallableBurdenPoints;
        var hotspotInputMetrics = MetricSet.From(
            (MetricIds.LongCallableCount, MetricValue.From(longCallableCount)),
            (MetricIds.HighCyclomaticComplexityCallableCount, MetricValue.From(highCyclomaticComplexityCallableCount)),
            (MetricIds.DeepNestingCallableCount, MetricValue.From(deepNestingCallableCount)),
            (MetricIds.LongParameterListCount, MetricValue.From(longParameterListCount)));
        var hotspotBreakdown = ProductMetricFormulas.TryComputeHotspots(hotspotInputMetrics, out var breakdown)
            ? breakdown
            : throw new InvalidOperationException("Hotspot breakdown should be available when hotspot counts are present.");

        sink.SetValue(MetricIds.LongCallableCount, longCallableCount);
        sink.SetValue(MetricIds.HighCyclomaticComplexityCallableCount, highCyclomaticComplexityCallableCount);
        sink.SetValue(MetricIds.DeepNestingCallableCount, deepNestingCallableCount);
        sink.SetValue(MetricIds.LongParameterListCount, longParameterListCount);
        sink.SetValue(MetricIds.CallableHotspotPoints, hotspotBreakdown.TotalPoints);
        sink.SetValue(MetricIds.CallableCount, callables.Count);
        sink.SetValue(MetricIds.AffectedCallableCount, affectedCallableCount);
        sink.SetValue(
            MetricIds.AffectedCallableRatio,
            callables.Count == 0 ? 0d : (double)affectedCallableCount / callables.Count);
        sink.SetValue(MetricIds.TotalCallableBurdenPoints, totalCallableBurdenPoints);
        sink.SetValue(MetricIds.TopCallableBurdenPoints, topCallableBurdenPoints);
        sink.SetValue(MetricIds.TopThreeCallableBurdenShare, topThreeCallableBurdenShare);
    }

    private static void SetAllNotApplicable(IMetricSink sink)
    {
        sink.SetNotApplicable(MetricIds.LongCallableCount);
        sink.SetNotApplicable(MetricIds.HighCyclomaticComplexityCallableCount);
        sink.SetNotApplicable(MetricIds.DeepNestingCallableCount);
        sink.SetNotApplicable(MetricIds.LongParameterListCount);
        sink.SetNotApplicable(MetricIds.CallableHotspotPoints);
        sink.SetNotApplicable(MetricIds.CallableCount);
        sink.SetNotApplicable(MetricIds.AffectedCallableCount);
        sink.SetNotApplicable(MetricIds.AffectedCallableRatio);
        sink.SetNotApplicable(MetricIds.TotalCallableBurdenPoints);
        sink.SetNotApplicable(MetricIds.TopCallableBurdenPoints);
        sink.SetNotApplicable(MetricIds.TopThreeCallableBurdenShare);
    }

    private static double ComputeCallableBurden(CallableSyntaxFact callable)
    {
        var lineCount = callable.Lines.EndLine1Based - callable.Lines.StartLine1Based + 1;
        var lineComplexityPressure = Math.Max(
            NormalizeLineComplexity(callable.CyclomaticComplexity, good: 3d, bad: 10d),
            NormalizeLineComplexity(callable.MaxNestingDepth, good: 1d, bad: 4d));
        var lineComplexityMultiplier = MinimumLineComplexityMultiplier +
            ((1d - MinimumLineComplexityMultiplier) * lineComplexityPressure);

        return
            (LineExcessWeight * Math.Max(0, lineCount - SoftCallableLines) * lineComplexityMultiplier) +
            (CyclomaticComplexityExcessWeight * Math.Max(0, callable.CyclomaticComplexity - SoftCyclomaticComplexity)) +
            (NestingDepthExcessWeight * Math.Max(0, callable.MaxNestingDepth - SoftNestingDepth)) +
            (ParameterCountExcessWeight * Math.Max(0, callable.ParameterCount - SoftParameterCount));
    }

    private static double NormalizeLineComplexity(double value, double good, double bad)
    {
        if (bad <= good)
        {
            throw new ArgumentOutOfRangeException(nameof(bad), "Normalization requires bad > good.");
        }

        return Math.Clamp((value - good) / (bad - good), 0d, 1d);
    }
}
