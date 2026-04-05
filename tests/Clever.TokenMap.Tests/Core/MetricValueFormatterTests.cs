using System.Globalization;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Tests.Core;

public sealed class MetricValueFormatterTests
{
    [Theory]
    [InlineData(128, "128")]
    [InlineData(1_200, "1.2K")]
    [InlineData(13_423, "13K")]
    [InlineData(1_300_000, "1.3M")]
    public void FormatCompact_CountMetrics_UsesHumanReadableUnits(long value, string expected)
    {
        var result = MetricValueFormatter.FormatCompact(MetricIds.Tokens, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(128, "128 B")]
    [InlineData(1_024, "1 KB")]
    [InlineData(171_801, "167.8 KB")]
    [InlineData(12 * 1024 * 1024, "12 MB")]
    public void FormatCompact_SizeMetric_UsesBinaryUnits(long value, string expected)
    {
        var result = MetricValueFormatter.FormatCompact(MetricIds.FileSizeBytes, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(3, "3")]
    [InlineData(18.5, "19")]
    [InlineData(1200.25, "1,200")]
    public void Format_ScoreMetric_RoundsToWholeNumber(double value, string expected)
    {
        var result = MetricValueFormatter.Format(MetricIds.ComplexityPoints, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(4, "4")]
    [InlineData(17.5, "18")]
    [InlineData(1_250, "1.2K")]
    public void FormatCompact_ScoreMetric_RoundsToWholeNumber(double value, string expected)
    {
        var result = MetricValueFormatter.FormatCompact(MetricIds.ComplexityPoints, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}

