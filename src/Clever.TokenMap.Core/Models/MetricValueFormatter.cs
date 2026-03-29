using System.Globalization;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Models;

public static class MetricValueFormatter
{
    public static string FormatCompact(AnalysisMetric metric, long value, CultureInfo? culture = null)
    {
        var effectiveCulture = culture ?? CultureInfo.CurrentCulture;

        return metric.Normalize() switch
        {
            AnalysisMetric.Size => FormatCompactSize(value, effectiveCulture),
            AnalysisMetric.Lines or AnalysisMetric.Tokens => FormatCompactCount(value, effectiveCulture),
            _ => FormatCompactCount(value, effectiveCulture),
        };
    }

    private static string FormatCompactCount(long value, CultureInfo culture)
    {
        var absoluteValue = Math.Abs((double)value);
        if (absoluteValue < 1_000d)
        {
            return value.ToString("N0", culture);
        }

        foreach (var (divisor, suffix) in new[] { (1_000_000_000_000d, "T"), (1_000_000_000d, "B"), (1_000_000d, "M"), (1_000d, "K") })
        {
            if (absoluteValue < divisor)
            {
                continue;
            }

            var scaled = value / divisor;
            var roundedWithoutDecimals = Math.Round(scaled, 0, MidpointRounding.AwayFromZero);
            if (Math.Abs(roundedWithoutDecimals) >= 1000d)
            {
                continue;
            }

            var format = Math.Abs(scaled) >= 10d ? "N0" : "N1";
            var text = scaled.ToString(format, culture);
            if (format == "N1" && text.EndsWith(culture.NumberFormat.NumberDecimalSeparator + "0", StringComparison.Ordinal))
            {
                text = scaled.ToString("N0", culture);
            }

            return $"{text}{suffix}";
        }

        return value.ToString("N0", culture);
    }

    private static string FormatCompactSize(long bytes, CultureInfo culture)
    {
        var absoluteValue = Math.Abs((double)bytes);
        if (absoluteValue < 1024d)
        {
            return $"{bytes.ToString("N0", culture)} B";
        }

        foreach (var (divisor, suffix) in new[] { (1024d * 1024d * 1024d * 1024d, "TB"), (1024d * 1024d * 1024d, "GB"), (1024d * 1024d, "MB"), (1024d, "KB") })
        {
            if (absoluteValue < divisor)
            {
                continue;
            }

            var scaled = bytes / divisor;
            var roundedWithoutDecimals = Math.Round(scaled, 0, MidpointRounding.AwayFromZero);
            if (Math.Abs(roundedWithoutDecimals) >= 1000d)
            {
                continue;
            }

            var text = scaled.ToString("N1", culture);
            if (text.EndsWith(culture.NumberFormat.NumberDecimalSeparator + "0", StringComparison.Ordinal))
            {
                text = scaled.ToString("N0", culture);
            }

            return $"{text} {suffix}";
        }

        return $"{bytes.ToString("N0", culture)} B";
    }
}
