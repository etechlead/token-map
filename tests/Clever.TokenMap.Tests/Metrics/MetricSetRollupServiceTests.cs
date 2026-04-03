using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class MetricSetRollupServiceTests
{
    private static readonly MetricId SumMetricId = new("sum_metric");
    private static readonly MetricId MaxMetricId = new("max_metric");
    private static readonly MetricId NoneMetricId = new("none_metric");

    [Fact]
    public void Rollup_AggregatesSumMaxAndNoneAsExpected()
    {
        var service = new MetricSetRollupService(new TestMetricCatalog(
        [
            new MetricDefinition(SumMetricId, "Sum", "Sum", MetricUnit.Count, MetricRollupKind.Sum, false, false, string.Empty),
            new MetricDefinition(MaxMetricId, "Max", "Max", MetricUnit.Count, MetricRollupKind.Max, false, false, string.Empty),
            new MetricDefinition(NoneMetricId, "None", "None", MetricUnit.Count, MetricRollupKind.None, false, false, string.Empty),
        ]));

        var result = service.Rollup(
        [
            MetricSet.From(
                (SumMetricId, MetricValue.From(2)),
                (MaxMetricId, MetricValue.From(3)),
                (NoneMetricId, MetricValue.From(5))),
            MetricSet.From(
                (SumMetricId, MetricValue.From(7)),
                (MaxMetricId, MetricValue.From(11)),
                (NoneMetricId, MetricValue.From(13))),
        ]);

        Assert.Equal(9, result.TryGetRoundedInt32(SumMetricId));
        Assert.Equal(11, result.TryGetRoundedInt32(MaxMetricId));
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(NoneMetricId).Status);
    }

    [Fact]
    public void Rollup_ReturnsNotApplicableWhenNoChildHasValue()
    {
        var service = new MetricSetRollupService(new TestMetricCatalog(
        [
            new MetricDefinition(SumMetricId, "Sum", "Sum", MetricUnit.Count, MetricRollupKind.Sum, false, false, string.Empty),
        ]));

        var result = service.Rollup(
        [
            MetricSet.From((SumMetricId, MetricValue.NotApplicable())),
            MetricSet.Empty,
        ]);

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(SumMetricId).Status);
    }

    private sealed class TestMetricCatalog(IReadOnlyList<MetricDefinition> definitions) : IMetricCatalog
    {
        private readonly Dictionary<MetricId, MetricDefinition> _definitionsById =
            definitions.ToDictionary(definition => definition.Id);

        public IReadOnlyList<MetricDefinition> GetAll() => definitions;

        public bool TryGet(MetricId id, out MetricDefinition definition) =>
            _definitionsById.TryGetValue(id, out definition!);
    }
}
