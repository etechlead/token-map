using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public sealed class MetricSetBuilder : IMetricSink
{
    private readonly Dictionary<MetricId, MetricValue> _values = [];

    public MetricSetBuilder()
    {
    }

    public MetricSetBuilder(MetricSet seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        foreach (var (id, value) in seed.Values)
        {
            _values[id] = value;
        }
    }

    public void SetValue(MetricId id, double value) => _values[id] = MetricValue.From(value);

    public void SetNotApplicable(MetricId id) => _values[id] = MetricValue.NotApplicable();

    public MetricSet Build() => new(new Dictionary<MetricId, MetricValue>(_values));
}
