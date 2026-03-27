using System.Globalization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

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
        var result = MetricValueFormatter.FormatCompact(AnalysisMetric.Tokens, value, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(128, "128 B")]
    [InlineData(1_024, "1 KB")]
    [InlineData(171_801, "167.8 KB")]
    [InlineData(12 * 1024 * 1024, "12 MB")]
    public void FormatCompact_SizeMetric_UsesBinaryUnits(long value, string expected)
    {
        var result = MetricValueFormatter.FormatCompact(AnalysisMetric.Size, value, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}
