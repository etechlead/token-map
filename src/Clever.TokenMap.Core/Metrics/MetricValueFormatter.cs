using System.Globalization;

namespace Clever.TokenMap.Core.Metrics;

public static class MetricValueFormatter
{
    public static string Format(MetricId metricId, MetricValue value, CultureInfo? culture = null, IMetricCatalog? metricCatalog = null)
    {
        return Format(metricCatalog.GetRequiredCatalogDefinition(metricId), value, compact: false, culture);
    }

    public static string FormatCompact(MetricId metricId, MetricValue value, CultureInfo? culture = null, IMetricCatalog? metricCatalog = null)
    {
        return Format(metricCatalog.GetRequiredCatalogDefinition(metricId), value, compact: true, culture);
    }

    private static MetricDefinition GetRequiredCatalogDefinition(this IMetricCatalog? metricCatalog, MetricId metricId) =>
        (metricCatalog ?? DefaultMetricCatalog.Instance).GetRequired(metricId);

    private static string Format(MetricDefinition definition, MetricValue value, bool compact, CultureInfo? culture)
    {
        if (!value.HasValue)
        {
            return "n/a";
        }

        var effectiveCulture = culture ?? CultureInfo.CurrentCulture;
        return definition.Unit switch
        {
            MetricUnit.Bytes => compact
                ? FormatCompactSize(value.Number, effectiveCulture)
                : FormatFullSize(value.Number, effectiveCulture),
            MetricUnit.Count => compact
                ? FormatCompactCount(value.Number, effectiveCulture)
                : FormatFullCount(value.Number, effectiveCulture),
            MetricUnit.Score => compact
                ? value.Number.ToString("N2", effectiveCulture)
                : value.Number.ToString("N2", effectiveCulture),
            _ => compact
                ? FormatCompactCount(value.Number, effectiveCulture)
                : FormatFullCount(value.Number, effectiveCulture),
        };
    }

    private static string FormatFullCount(double value, CultureInfo culture) =>
        checked((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString("N0", culture);

    private static string FormatCompactCount(double value, CultureInfo culture)
    {
        var roundedValue = checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        var absoluteValue = Math.Abs((double)roundedValue);
        if (absoluteValue < 1_000d)
        {
            return roundedValue.ToString("N0", culture);
        }

        foreach (var (divisor, suffix) in new[] { (1_000_000_000_000d, "T"), (1_000_000_000d, "B"), (1_000_000d, "M"), (1_000d, "K") })
        {
            if (absoluteValue < divisor)
            {
                continue;
            }

            var scaled = roundedValue / divisor;
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

        return roundedValue.ToString("N0", culture);
    }

    private static string FormatFullSize(double value, CultureInfo culture)
    {
        var roundedBytes = checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        var absoluteValue = Math.Abs((double)roundedBytes);
        return absoluteValue switch
        {
            >= 1024d * 1024d * 1024d => $"{roundedBytes / 1024d / 1024d / 1024d:F1} GB",
            >= 1024d * 1024d => $"{roundedBytes / 1024d / 1024d:F1} MB",
            >= 1024d => $"{roundedBytes / 1024d:F1} KB",
            _ => $"{roundedBytes.ToString("N0", culture)} B",
        };
    }

    private static string FormatCompactSize(double value, CultureInfo culture)
    {
        var roundedBytes = checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        var absoluteValue = Math.Abs((double)roundedBytes);
        if (absoluteValue < 1024d)
        {
            return $"{roundedBytes.ToString("N0", culture)} B";
        }

        foreach (var (divisor, suffix) in new[] { (1024d * 1024d * 1024d * 1024d, "TB"), (1024d * 1024d * 1024d, "GB"), (1024d * 1024d, "MB"), (1024d, "KB") })
        {
            if (absoluteValue < divisor)
            {
                continue;
            }

            var scaled = roundedBytes / divisor;
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

        return $"{roundedBytes.ToString("N0", culture)} B";
    }
}
