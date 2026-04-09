using System;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.App.Services;

public sealed class MetricPresentationCatalog
{
    private readonly LocalizationState _localization;

    public MetricPresentationCatalog(LocalizationState localization)
    {
        _localization = localization;
        _localization.LanguageChanged += (_, _) => PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PresentationChanged;

    public string GetDisplayName(MetricId metricId)
    {
        if (DefaultMetricCatalog.Instance.TryGet(metricId, out var definition))
        {
            return _localization.GetMetricDisplayName(metricId.Value, definition.DisplayName);
        }

        return metricId.Value;
    }

    public string GetShortName(MetricId metricId)
    {
        if (DefaultMetricCatalog.Instance.TryGet(metricId, out var definition))
        {
            return _localization.GetMetricShortName(metricId.Value, definition.ShortName);
        }

        return metricId.Value;
    }

    public string GetDescription(MetricId metricId)
    {
        if (DefaultMetricCatalog.Instance.TryGet(metricId, out var definition))
        {
            return _localization.GetMetricDescription(metricId.Value, definition.Description);
        }

        return metricId.Value;
    }
}
