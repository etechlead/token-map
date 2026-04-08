using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Tests.Core;

public sealed class AppSettingsCanonicalizerTests
{
    [Fact]
    public void NormalizeVisibleMetricIds_DropsInternalStructuralRiskMetric()
    {
        var normalized = AppSettingsCanonicalizer.NormalizeVisibleMetricIds(
        [
            MetricIds.Tokens,
            MetricIds.ComplexityPoints,
            MetricIds.RefactorPriorityPoints,
        ]);

        Assert.Equal(
        [
            MetricIds.Tokens,
            MetricIds.RefactorPriorityPoints,
        ], normalized);
    }

    [Fact]
    public void Normalize_FallsBackToVisibleMetricWhenSelectedMetricIsInternalOnly()
    {
        var settings = new AppSettings
        {
            Analysis =
            {
                SelectedMetric = MetricIds.ComplexityPoints,
                VisibleMetricIds =
                [
                    MetricIds.ComplexityPoints,
                ],
            },
        };

        var normalized = AppSettingsCanonicalizer.Normalize(settings);

        Assert.DoesNotContain(normalized.Analysis.VisibleMetricIds, metricId => metricId == MetricIds.ComplexityPoints);
        Assert.Equal(MetricIds.Tokens, normalized.Analysis.SelectedMetric);
    }
}
