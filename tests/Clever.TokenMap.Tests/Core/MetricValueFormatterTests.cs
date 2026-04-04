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
    [InlineData(0, "0.0%")]
    [InlineData(0.375, "37.5%")]
    [InlineData(1, "100.0%")]
    public void Format_PercentMetric_UsesPercentUnits(double value, string expected)
    {
        var result = MetricValueFormatter.Format(MetricIds.CommentRatio, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(3, "3.00")]
    [InlineData(18.5, "18.50")]
    [InlineData(1200.25, "1,200.25")]
    public void Format_AverageCountMetric_PreservesFractionalPrecision(double value, string expected)
    {
        var result = MetricValueFormatter.Format(MetricIds.AverageCallableLines, MetricValue.From(value), CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}
