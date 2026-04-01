namespace Clever.TokenMap.Core.Metrics;

public sealed class MetricSet
{
    private readonly IReadOnlyDictionary<MetricId, MetricValue> _values;

    public static MetricSet Empty { get; } = new(new Dictionary<MetricId, MetricValue>());

    public static MetricSet From(params (MetricId Id, MetricValue Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var dictionary = new Dictionary<MetricId, MetricValue>();
        foreach (var (id, value) in values)
        {
            dictionary[id] = value;
        }

        return new MetricSet(dictionary);
    }

    public MetricSet(IReadOnlyDictionary<MetricId, MetricValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values;
    }

    public IReadOnlyDictionary<MetricId, MetricValue> Values => _values;

    public MetricValue GetOrDefault(MetricId id) =>
        _values.TryGetValue(id, out var value)
            ? value
            : MetricValue.NotApplicable();
}
