using System;
using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class ToolbarViewModel : ViewModelBase
{
    public ToolbarViewModel(
        IAsyncRelayCommand openFolderCommand,
        IAsyncRelayCommand rescanCommand,
        IRelayCommand cancelCommand)
    {
        OpenFolderCommand = openFolderCommand;
        RescanCommand = rescanCommand;
        CancelCommand = cancelCommand;
    }

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RescanCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IReadOnlyList<string> MetricOptions { get; } =
    [
        "Tokens",
        "Total lines",
        "Code lines",
    ];

    public IReadOnlyList<string> TokenProfiles { get; } =
    [
        "o200k_base",
        "cl100k_base",
        "p50k_base",
    ];

    [ObservableProperty]
    private bool canConfigureScanOptions = true;

    [ObservableProperty]
    private bool canChangeMetric;

    [ObservableProperty]
    private string selectedFolderDisplay = "No folder selected";

    [ObservableProperty]
    private string selectedMetric = "Tokens";

    [ObservableProperty]
    private string selectedTokenProfile = "o200k_base";

    [ObservableProperty]
    private bool respectGitIgnore = true;

    [ObservableProperty]
    private bool respectIgnore = true;

    [ObservableProperty]
    private bool useDefaultExcludes = true;

    public void UpdateFolder(string? folderPath)
    {
        SelectedFolderDisplay = string.IsNullOrWhiteSpace(folderPath)
            ? "No folder selected"
            : folderPath;
    }

    public void RefreshAvailability(bool hasSelectedFolder, bool isBusy, bool hasSnapshot)
    {
        CanConfigureScanOptions = !isBusy;
        CanChangeMetric = hasSnapshot && !isBusy;
        OpenFolderCommand.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public ScanOptions BuildScanOptions() =>
        new()
        {
            TokenProfile = SelectedTokenProfile switch
            {
                "cl100k_base" => TokenProfile.Cl100KBase,
                "p50k_base" => TokenProfile.P50KBase,
                _ => TokenProfile.O200KBase,
            },
            RespectGitIgnore = RespectGitIgnore,
            RespectDotIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };

    public AnalysisSettings BuildAnalysisSettings() =>
        new()
        {
            SelectedMetric = SelectedMetric,
            SelectedTokenProfile = SelectedTokenProfile,
            RespectGitIgnore = RespectGitIgnore,
            RespectIgnore = RespectIgnore,
            UseDefaultExcludes = UseDefaultExcludes,
        };

    public void ApplyAnalysisSettings(AnalysisSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SelectedMetric = IsKnownMetric(settings.SelectedMetric)
            ? settings.SelectedMetric
            : "Tokens";
        SelectedTokenProfile = IsKnownTokenProfile(settings.SelectedTokenProfile)
            ? settings.SelectedTokenProfile
            : "o200k_base";
        RespectGitIgnore = settings.RespectGitIgnore;
        RespectIgnore = settings.RespectIgnore;
        UseDefaultExcludes = settings.UseDefaultExcludes;
    }

    private bool IsKnownMetric(string value) =>
        MetricOptions.Contains(value, StringComparer.Ordinal);

    private bool IsKnownTokenProfile(string value) =>
        TokenProfiles.Contains(value, StringComparer.Ordinal);
}
