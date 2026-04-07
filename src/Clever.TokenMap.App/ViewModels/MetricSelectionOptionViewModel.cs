using System;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public sealed class MetricSelectionOptionViewModel : ObservableObject
{
    private readonly Action<MetricId> _selectMetric;
    private bool _isSelected;
    private bool _isSyncing;

    public MetricSelectionOptionViewModel(MetricDefinition definition, Action<MetricId> selectMetric)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _selectMetric = selectMetric ?? throw new ArgumentNullException(nameof(selectMetric));
    }

    public MetricDefinition Definition { get; }

    public string Label => Definition.ShortName;

    public string Description => Definition.Description;

    public bool IsFirst { get; private set; }

    public bool IsMiddle { get; private set; }

    public bool IsLast { get; private set; }

    public bool ShowLeadingDivider => !IsFirst;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value) || _isSyncing || !value)
            {
                return;
            }

            _selectMetric(Definition.Id);
        }
    }

    internal void Sync(bool isSelected, int index, int count)
    {
        _isSyncing = true;
        try
        {
            IsSelected = isSelected;
        }
        finally
        {
            _isSyncing = false;
        }

        SetPositionClasses(index, count);
    }

    private void SetPositionClasses(int index, int count)
    {
        var isFirst = index == 0;
        var isLast = index == count - 1;
        var isMiddle = !isFirst && !isLast;

        if (IsFirst != isFirst)
        {
            IsFirst = isFirst;
            OnPropertyChanged(nameof(IsFirst));
            OnPropertyChanged(nameof(ShowLeadingDivider));
        }

        if (IsMiddle != isMiddle)
        {
            IsMiddle = isMiddle;
            OnPropertyChanged(nameof(IsMiddle));
        }

        if (IsLast != isLast)
        {
            IsLast = isLast;
            OnPropertyChanged(nameof(IsLast));
        }
    }
}
