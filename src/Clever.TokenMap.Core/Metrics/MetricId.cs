namespace Clever.TokenMap.Core.Metrics;

public readonly record struct MetricId(string Value)
{
    public override string ToString() => Value;
}
