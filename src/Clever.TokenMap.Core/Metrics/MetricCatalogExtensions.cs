namespace Clever.TokenMap.Core.Metrics;

public static class MetricCatalogExtensions
{
    public static MetricDefinition GetRequired(this IMetricCatalog catalog, MetricId metricId)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.TryGet(metricId, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"Unknown metric id '{metricId.Value}'.");
    }
}
