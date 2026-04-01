namespace Clever.TokenMap.Core.Metrics;

public readonly record struct MetricValue(
    double Number,
    MetricStatus Status = MetricStatus.Ok,
    string? Error = null)
{
    public static MetricValue From(double value) => new(value);

    public static MetricValue NotApplicable() => new(0, MetricStatus.NotApplicable);

    public static MetricValue Failed(string? error = null) => new(0, MetricStatus.Failed, error);

    public bool HasValue => Status == MetricStatus.Ok;
}
