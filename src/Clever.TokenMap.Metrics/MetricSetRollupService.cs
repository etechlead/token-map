using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public sealed class MetricSetRollupService
{
    private readonly IMetricCatalog _metricCatalog;

    public MetricSetRollupService(IMetricCatalog metricCatalog)
    {
        _metricCatalog = metricCatalog ?? throw new ArgumentNullException(nameof(metricCatalog));
    }

    public MetricSet Rollup(IEnumerable<MetricSet> childMetricSets)
    {
        ArgumentNullException.ThrowIfNull(childMetricSets);

        var materializedChildMetricSets = childMetricSets.ToArray();
        var builder = new MetricSetBuilder();

        foreach (var definition in _metricCatalog.GetAll())
        {
            var values = materializedChildMetricSets
                .Select(metricSet => metricSet.GetOrDefault(definition.Id))
                .Where(metricValue => metricValue.HasValue)
                .Select(metricValue => metricValue.Number)
                .ToArray();

            if (values.Length == 0)
            {
                builder.SetNotApplicable(definition.Id);
                continue;
            }

            switch (definition.DirectoryRollup)
            {
                case MetricRollupKind.Sum:
                    builder.SetValue(definition.Id, values.Sum());
                    break;
                case MetricRollupKind.Max:
                    builder.SetValue(definition.Id, values.Max());
                    break;
                case MetricRollupKind.None:
                    builder.SetNotApplicable(definition.Id);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported rollup kind '{definition.DirectoryRollup}'.");
            }
        }

        return builder.Build();
    }
}
