using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public sealed class MetricSetBuilder : IMetricSink
{
    private readonly Dictionary<MetricId, MetricValue> _values = [];

    public void SetValue(MetricId id, double value) => _values[id] = MetricValue.From(value);

    public void SetNotApplicable(MetricId id) => _values[id] = MetricValue.NotApplicable();

    public MetricSet Build() => new(_values);
}
