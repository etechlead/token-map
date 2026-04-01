using System;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public sealed class MetricVisibilityOptionViewModel : ObservableObject
{
    private readonly Action<MetricId, bool> _setVisibility;
    private bool _isVisible;
    private bool _isToggleEnabled = true;
    private bool _isSyncing;

    public MetricVisibilityOptionViewModel(MetricDefinition definition, Action<MetricId, bool> setVisibility)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _setVisibility = setVisibility ?? throw new ArgumentNullException(nameof(setVisibility));
    }

    public MetricDefinition Definition { get; }

    public string Label => Definition.ShortName;

    public string Description => Definition.Description;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (!SetProperty(ref _isVisible, value) || _isSyncing)
            {
                return;
            }

            _setVisibility(Definition.Id, value);
        }
    }

    public bool IsToggleEnabled
    {
        get => _isToggleEnabled;
        private set => SetProperty(ref _isToggleEnabled, value);
    }

    internal void Sync(bool isVisible, bool isToggleEnabled)
    {
        _isSyncing = true;
        try
        {
            IsVisible = isVisible;
        }
        finally
        {
            _isSyncing = false;
        }

        IsToggleEnabled = isToggleEnabled;
    }
}
