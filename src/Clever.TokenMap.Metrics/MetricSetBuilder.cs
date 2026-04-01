using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public sealed class MetricSetBuilder : IMetricSink
{
    private readonly Dictionary<MetricId, MetricValue> _values = [];

    public void SetValue(MetricId id, double value) => _values[id] = MetricValue.From(value);

    public void SetNotApplicable(MetricId id) => _values[id] = MetricValue.NotApplicable();

    public void SetFailure(MetricId id, string? failureMessage = null) => _values[id] = MetricValue.Failed(failureMessage);

    public MetricSet Build() => new(_values);
}
