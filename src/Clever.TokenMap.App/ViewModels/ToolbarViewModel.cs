using System.ComponentModel;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase, IToolbarAvailabilitySink
{
    private readonly RelayCommand _selectDarkThemePreferenceCommand;
    private readonly RelayCommand _selectLightThemePreferenceCommand;
    private readonly RelayCommand _selectSystemThemePreferenceCommand;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly IReadOnlyCurrentFolderSettingsState _currentFolderSettingsState;
    private readonly IReadOnlySettingsState _settingsState;

    public ToolbarViewModel(
        ISettingsCoordinator settingsCoordinator,
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        _settingsCoordinator = settingsCoordinator;
        _settingsState = settingsCoordinator.State;
        _currentFolderSettingsState = settingsCoordinator.CurrentFolderState;
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
        _settingsState.PropertyChanged += SettingsStateOnPropertyChanged;
        _currentFolderSettingsState.PropertyChanged += CurrentFolderSettingsStateOnPropertyChanged;
        _selectSystemThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.System));
        _selectLightThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Light));
        _selectDarkThemePreferenceCommand = new RelayCommand(() => SelectThemePreference(ThemePreference.Dark));
    }

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RescanCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand SelectSystemThemePreferenceCommand => _selectSystemThemePreferenceCommand;

    public IRelayCommand SelectLightThemePreferenceCommand => _selectLightThemePreferenceCommand;

    public IRelayCommand SelectDarkThemePreferenceCommand => _selectDarkThemePreferenceCommand;

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private bool isStopVisible;

    [ObservableProperty]
    private bool isRescanVisible;

    public bool IsTokensMetricSelected
    {
        get => SelectedMetric == AnalysisMetric.Tokens;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.Tokens;
            }
        }
    }

    public bool IsLinesMetricSelected
    {
        get => SelectedMetric == AnalysisMetric.Lines;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.Lines;
            }
        }
    }

    public bool IsSizeMetricSelected
    {
        get => SelectedMetric == AnalysisMetric.Size;
        set
        {
            if (value)
            {
                SelectedMetric = AnalysisMetric.Size;
            }
        }
    }

    public AnalysisMetric SelectedMetric
    {
        get => _settingsState.SelectedMetric;
        set => _settingsCoordinator.SetSelectedMetric(value);
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
        ? $"Current folder: {FolderDisplayText.GetFolderDisplayName(_currentFolderSettingsState.ActiveRootPath)}"
        : "Current folder";

    public ThemePreference SelectedThemePreference
    {
        get => _settingsState.SelectedThemePreference;
        set => _settingsCoordinator.SetThemePreference(value);
    }

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

    private void SettingsStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IReadOnlySettingsState.SelectedMetric):
                OnPropertyChanged(nameof(SelectedMetric));
                OnPropertyChanged(nameof(IsTokensMetricSelected));
                OnPropertyChanged(nameof(IsLinesMetricSelected));
                OnPropertyChanged(nameof(IsSizeMetricSelected));
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
}
