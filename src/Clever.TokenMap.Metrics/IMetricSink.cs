using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Metrics;

public interface IMetricSink
{
    void SetValue(MetricId id, double value);

    void SetNotApplicable(MetricId id);
}
