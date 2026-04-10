using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase, IToolbarAvailabilitySink
{
    private readonly RelayCommand _selectDarkThemePreferenceCommand;
    private readonly RelayCommand _selectLightThemePreferenceCommand;
    private readonly RelayCommand _selectSystemThemePreferenceCommand;
    private readonly RelayCommand _toggleWorkspaceLayoutCommand;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly IReadOnlyCurrentFolderSettingsState _currentFolderSettingsState;
    private readonly IReadOnlySettingsState _settingsState;
    private readonly LocalizationState _localization;
    private readonly MetricPresentationCatalog _metricPresentationCatalog;
    private readonly IReadOnlyList<MetricDefinition> _allMetricDefinitions;
    private readonly ObservableCollection<MetricSelectionOptionViewModel> _visibleTreemapMetricOptions = [];
    private readonly ObservableCollection<MetricVisibilityOptionViewModel> _metricVisibilityOptions = [];
    private readonly ObservableCollection<MetricVisibilityOptionViewModel> _treeOnlyMetricVisibilityOptions = [];
    private IReadOnlyList<MetricDefinition> _visibleMetricDefinitions = [];
    private MetricSelectionOptionViewModel? _selectedTreemapMetricOption;

    public ToolbarViewModel(
        ISettingsCoordinator settingsCoordinator,
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand,
        LocalizationState localization,
        MetricPresentationCatalog metricPresentationCatalog)
    {
        _settingsCoordinator = settingsCoordinator;
        _settingsState = settingsCoordinator.State;
        _currentFolderSettingsState = settingsCoordinator.CurrentFolderState;
        _localization = localization;
        _metricPresentationCatalog = metricPresentationCatalog;
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _settingsState.PropertyChanged += SettingsStateOnPropertyChanged;
        _currentFolderSettingsState.PropertyChanged += CurrentFolderSettingsStateOnPropertyChanged;
        _localization.LanguageChanged += LocalizationOnLanguageChanged;
        _metricPresentationCatalog.PresentationChanged += MetricPresentationCatalogOnPresentationChanged;
        _selectSystemThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.System));
        _selectLightThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Light));
        _selectDarkThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Dark));
        _toggleWorkspaceLayoutCommand = new RelayCommand(ToggleWorkspaceLayout);
        _allMetricDefinitions = DefaultMetricCatalog.GetUserVisibleDefinitions();

        VisibleTreemapMetricOptions = new ReadOnlyObservableCollection<MetricSelectionOptionViewModel>(_visibleTreemapMetricOptions);
        MetricVisibilityOptions = new ReadOnlyObservableCollection<MetricVisibilityOptionViewModel>(_metricVisibilityOptions);
        TreeOnlyMetricVisibilityOptions = new ReadOnlyObservableCollection<MetricVisibilityOptionViewModel>(_treeOnlyMetricVisibilityOptions);
        foreach (var definition in _allMetricDefinitions)
        {
            var option = new MetricVisibilityOptionViewModel(
                definition,
                _settingsCoordinator.SetMetricVisibility,
                _metricPresentationCatalog);
            if (definition.SupportsTreemapWeight)
            {
                _metricVisibilityOptions.Add(option);
            }
            else
            {
                _treeOnlyMetricVisibilityOptions.Add(option);
            }
        }

        RefreshMetricPresentation();
    }

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RescanCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand SelectSystemThemePreferenceCommand => _selectSystemThemePreferenceCommand;

    public IRelayCommand SelectLightThemePreferenceCommand => _selectLightThemePreferenceCommand;

    public IRelayCommand SelectDarkThemePreferenceCommand => _selectDarkThemePreferenceCommand;

    public IRelayCommand ToggleWorkspaceLayoutCommand => _toggleWorkspaceLayoutCommand;

    public ReadOnlyObservableCollection<MetricSelectionOptionViewModel> VisibleTreemapMetricOptions { get; }

    public ReadOnlyObservableCollection<MetricVisibilityOptionViewModel> MetricVisibilityOptions { get; }

    public ReadOnlyObservableCollection<MetricVisibilityOptionViewModel> TreeOnlyMetricVisibilityOptions { get; }

    public ReadOnlyCollection<ApplicationLanguageOption> ApplicationLanguageOptions => _localization.ApplicationLanguageOptions;

    public MetricPresentationCatalog MetricPresentationCatalog => _metricPresentationCatalog;

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private bool isStopVisible;

    [ObservableProperty]
    private bool isRescanVisible;

    public MetricId SelectedMetric
    {
        get => _settingsState.SelectedMetric;
        set => _settingsCoordinator.SetSelectedMetric(value);
    }

    public IReadOnlyList<MetricId> VisibleMetricIds => _settingsState.VisibleMetricIds;

    public IReadOnlyList<MetricDefinition> VisibleMetricDefinitions => _visibleMetricDefinitions;

    public bool HasTreeOnlyMetricVisibilityOptions => TreeOnlyMetricVisibilityOptions.Count > 0;

    public bool ShowTreemapMetricButtons => VisibleTreemapMetricOptions.Count <= 7;

    public bool ShowTreemapMetricSelector => VisibleTreemapMetricOptions.Count > 7;

    public MetricSelectionOptionViewModel? SelectedTreemapMetricOption
    {
        get => _selectedTreemapMetricOption;
        set
        {
            if (ReferenceEquals(_selectedTreemapMetricOption, value))
            {
                return;
            }

            _selectedTreemapMetricOption = value;
            OnPropertyChanged(nameof(SelectedTreemapMetricOption));
            if (value is not null && value.Definition.Id != SelectedMetric)
            {
                SelectedMetric = value.Definition.Id;
            }
        }
    }

    public bool ShowTreemapMetricValues
    {
        get => _settingsState.ShowTreemapMetricValues;
        set => _settingsCoordinator.SetShowTreemapMetricValues(value);
    }

    public bool RespectGitIgnore
    {
        get => _settingsState.RespectGitIgnore;
        set => _settingsCoordinator.SetRespectGitIgnore(value);
    }

    public bool UseGlobalExcludes
    {
        get => _settingsState.UseGlobalExcludes;
        set => _settingsCoordinator.SetUseGlobalExcludes(value);
    }

    public bool UseFolderExcludes
    {
        get => _currentFolderSettingsState.UseFolderExcludes;
        set => _settingsCoordinator.SetUseFolderExcludes(value);
    }

    public bool HasCurrentFolderSettings => _currentFolderSettingsState.HasActiveFolder;

    public bool CanConfigureFolderExcludes => CanConfigureScanOptions && HasCurrentFolderSettings;

    public string CurrentFolderSettingsTitle => HasCurrentFolderSettings
        ? _localization.FormatCurrentFolderSettingsTitle(FolderDisplayText.GetFolderDisplayName(_currentFolderSettingsState.ActiveRootPath))
        : _localization.CurrentFolderSettingsTitleFallback;

    public ApplicationLanguageOption? SelectedApplicationLanguageOption
    {
        get => ApplicationLanguageOptions.FirstOrDefault(option =>
            string.Equals(option.Value, _settingsState.ApplicationLanguageTag, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null ||
                string.Equals(value.Value, _settingsState.ApplicationLanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _settingsCoordinator.SetApplicationLanguageTag(value.Value);
        }
    }

    public ThemePreference SelectedThemePreference
    {
        get => _settingsState.SelectedThemePreference;
        set => _settingsCoordinator.SetThemePreference(value);
    }

    public WorkspaceLayoutMode SelectedWorkspaceLayoutMode
    {
        get => _settingsState.WorkspaceLayoutMode;
        set => _settingsCoordinator.SetWorkspaceLayoutMode(value);
    }

    public bool IsSideBySideWorkspaceLayout => SelectedWorkspaceLayoutMode == WorkspaceLayoutMode.SideBySide;

    public bool IsStackedWorkspaceLayout
    {
        get => SelectedWorkspaceLayoutMode == WorkspaceLayoutMode.Stacked;
        set => SelectedWorkspaceLayoutMode = value
            ? WorkspaceLayoutMode.Stacked
            : WorkspaceLayoutMode.SideBySide;
    }

    public string WorkspaceLayoutToggleToolTip => IsStackedWorkspaceLayout
        ? _localization.WorkspaceLayoutToggleToolTip(isStackedWorkspaceLayout: true)
        : _localization.WorkspaceLayoutToggleToolTip(isStackedWorkspaceLayout: false);

    public TreemapPalette SelectedTreemapPalette
    {
        get => _settingsState.SelectedTreemapPalette;
        set => _settingsCoordinator.SetTreemapPalette(value);
    }

    public bool IsSystemThemeSelected => SelectedThemePreference == ThemePreference.System;

    public bool IsLightThemeSelected => SelectedThemePreference == ThemePreference.Light;

    public bool IsDarkThemeSelected => SelectedThemePreference == ThemePreference.Dark;

    public bool IsPlainTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Plain;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Plain;
            }
        }
    }

    public bool IsWeightedTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Weighted;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Weighted;
            }
        }
    }

    public bool IsStudioTreemapPaletteSelected
    {
        get => SelectedTreemapPalette == TreemapPalette.Studio;
        set
        {
            if (value)
            {
                SelectedTreemapPalette = TreemapPalette.Studio;
            }
        }
    }

    public void RefreshAvailability(bool isBusy, bool hasSnapshot)
    {
        CanConfigureScanOptions = !isBusy;
        CanChangeMetric = hasSnapshot && !isBusy;
        IsStopVisible = isBusy;
        IsRescanVisible = hasSnapshot && !isBusy;
        OnPropertyChanged(nameof(CanConfigureFolderExcludes));
        OpenFolderCommand.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void SelectThemePreference(ThemePreference value)
    {
        SelectedThemePreference = value;
    }

    private void ToggleWorkspaceLayout()
    {
        SelectedWorkspaceLayoutMode = IsSideBySideWorkspaceLayout
            ? WorkspaceLayoutMode.Stacked
            : WorkspaceLayoutMode.SideBySide;
    }

    private void SettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IReadOnlySettingsState.SelectedMetric):
                OnPropertyChanged(nameof(SelectedMetric));
                RefreshMetricSelection();
                break;
            case nameof(IReadOnlySettingsState.VisibleMetricIds):
                OnPropertyChanged(nameof(VisibleMetricIds));
                RefreshMetricPresentation();
                break;
            case nameof(IReadOnlySettingsState.RespectGitIgnore):
                OnPropertyChanged(nameof(RespectGitIgnore));
                break;
            case nameof(IReadOnlySettingsState.UseGlobalExcludes):
                OnPropertyChanged(nameof(UseGlobalExcludes));
                break;
            case nameof(IReadOnlySettingsState.SelectedThemePreference):
                OnPropertyChanged(nameof(SelectedThemePreference));
                OnPropertyChanged(nameof(IsSystemThemeSelected));
                OnPropertyChanged(nameof(IsLightThemeSelected));
                OnPropertyChanged(nameof(IsDarkThemeSelected));
                break;
            case nameof(IReadOnlySettingsState.ApplicationLanguageTag):
                OnPropertyChanged(nameof(SelectedApplicationLanguageOption));
                break;
            case nameof(IReadOnlySettingsState.WorkspaceLayoutMode):
                OnPropertyChanged(nameof(SelectedWorkspaceLayoutMode));
                OnPropertyChanged(nameof(IsSideBySideWorkspaceLayout));
                OnPropertyChanged(nameof(IsStackedWorkspaceLayout));
                OnPropertyChanged(nameof(WorkspaceLayoutToggleToolTip));
                break;
            case nameof(IReadOnlySettingsState.SelectedTreemapPalette):
                OnPropertyChanged(nameof(SelectedTreemapPalette));
                OnPropertyChanged(nameof(IsPlainTreemapPaletteSelected));
                OnPropertyChanged(nameof(IsWeightedTreemapPaletteSelected));
                OnPropertyChanged(nameof(IsStudioTreemapPaletteSelected));
                break;
            case nameof(IReadOnlySettingsState.ShowTreemapMetricValues):
                OnPropertyChanged(nameof(ShowTreemapMetricValues));
                break;
        }
    }

    private void CurrentFolderSettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IReadOnlyCurrentFolderSettingsState.ActiveRootPath):
                OnPropertyChanged(nameof(HasCurrentFolderSettings));
                OnPropertyChanged(nameof(CanConfigureFolderExcludes));
                OnPropertyChanged(nameof(CurrentFolderSettingsTitle));
                break;
            case nameof(IReadOnlyCurrentFolderSettingsState.UseFolderExcludes):
                OnPropertyChanged(nameof(UseFolderExcludes));
                break;
        }
    }

    public ScanOptions BuildScanOptions() =>
        _settingsCoordinator.BuildCurrentScanOptions();

    private void LocalizationOnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentFolderSettingsTitle));
        OnPropertyChanged(nameof(WorkspaceLayoutToggleToolTip));
        OnPropertyChanged(nameof(SelectedApplicationLanguageOption));
    }

    private void MetricPresentationCatalogOnPresentationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(VisibleMetricDefinitions));
    }

    private void RefreshMetricPresentation()
    {
        var visibleMetricIdSet = _settingsState.VisibleMetricIds.ToHashSet();
        _visibleMetricDefinitions = _allMetricDefinitions
            .Where(definition => visibleMetricIdSet.Contains(definition.Id))
            .ToArray();
        OnPropertyChanged(nameof(VisibleMetricDefinitions));

        RefreshMetricVisibilityOptions(visibleMetricIdSet);
        RebuildVisibleTreemapMetricOptions();
        OnPropertyChanged(nameof(ShowTreemapMetricButtons));
        OnPropertyChanged(nameof(ShowTreemapMetricSelector));
    }

    private void RefreshMetricVisibilityOptions(HashSet<MetricId> visibleMetricIdSet)
    {
        foreach (var option in _metricVisibilityOptions.Concat(_treeOnlyMetricVisibilityOptions))
        {
            var isVisible = visibleMetricIdSet.Contains(option.Definition.Id);
            var isToggleEnabled = isVisible
                ? visibleMetricIdSet.Count > 1
                : true;
            option.Sync(isVisible, isToggleEnabled);
        }
    }

    private void RebuildVisibleTreemapMetricOptions()
    {
        _visibleTreemapMetricOptions.Clear();
        var visibleTreemapMetrics = _visibleMetricDefinitions
            .Where(definition => definition.SupportsTreemapWeight)
            .ToArray();
        for (var index = 0; index < visibleTreemapMetrics.Length; index++)
        {
            var definition = visibleTreemapMetrics[index];
            var option = new MetricSelectionOptionViewModel(
                definition,
                metricId => SelectedMetric = metricId,
                _metricPresentationCatalog);
            option.Sync(definition.Id == SelectedMetric, index, visibleTreemapMetrics.Length);
            _visibleTreemapMetricOptions.Add(option);
        }

        RefreshMetricSelection();
    }

    private void RefreshMetricSelection()
    {
        MetricSelectionOptionViewModel? selectedOption = null;
        for (var index = 0; index < _visibleTreemapMetricOptions.Count; index++)
        {
            var option = _visibleTreemapMetricOptions[index];
            var isSelected = option.Definition.Id == SelectedMetric;
            option.Sync(isSelected, index, _visibleTreemapMetricOptions.Count);
            if (isSelected)
            {
                selectedOption = option;
            }
        }

        if (!ReferenceEquals(_selectedTreemapMetricOption, selectedOption))
        {
            _selectedTreemapMetricOption = selectedOption;
            OnPropertyChanged(nameof(SelectedTreemapMetricOption));
        }
    }
}
