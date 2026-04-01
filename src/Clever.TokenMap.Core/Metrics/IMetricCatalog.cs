namespace Clever.TokenMap.Core.Metrics;

public interface IMetricCatalog
{
    IReadOnlyList<MetricDefinition> GetAll();

    bool TryGet(MetricId id, out MetricDefinition definition);
}
