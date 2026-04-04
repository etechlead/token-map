using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class MetricSetRollupServiceTests
{
    private static readonly MetricId SumMetricId = new("sum_metric");

    [Fact]
    public void Rollup_SumsAllDefinedMetrics()
    {
        var service = new MetricSetRollupService(new TestMetricCatalog(
        [
            new MetricDefinition(SumMetricId, "Sum", "Sum", MetricUnit.Count, MetricRollupKind.Sum, false, false, string.Empty),
        ]));

        var result = service.Rollup(
        [
            MetricSet.From(
                (SumMetricId, MetricValue.From(2))),
            MetricSet.From(
                (SumMetricId, MetricValue.From(7))),
        ]);

        Assert.Equal(9, result.TryGetRoundedInt32(SumMetricId));
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
