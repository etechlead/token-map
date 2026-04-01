namespace Clever.TokenMap.Core.Metrics;

public static class MetricSetNumericExtensions
{
    public static double? TryGetNumber(this MetricSet metricSet, MetricId metricId)
    {
        ArgumentNullException.ThrowIfNull(metricSet);

        var metricValue = metricSet.GetOrDefault(metricId);
        return metricValue.HasValue
            ? metricValue.Number
            : null;
    }

    public static long? TryGetRoundedInt64(this MetricSet metricSet, MetricId metricId)
    {
        var value = metricSet.TryGetNumber(metricId);
        return value.HasValue
            ? checked((long)Math.Round(value.Value, MidpointRounding.AwayFromZero))
            : null;
    }

    public static int? TryGetRoundedInt32(this MetricSet metricSet, MetricId metricId)
    {
        var value = metricSet.TryGetNumber(metricId);
        return value.HasValue
            ? checked((int)Math.Round(value.Value, MidpointRounding.AwayFromZero))
            : null;
    }
}
