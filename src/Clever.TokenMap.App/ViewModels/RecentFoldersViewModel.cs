using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed partial class RecentFoldersViewModel : ViewModelBase
{
    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly IFolderPathService _folderPathService;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly Func<ScanOptions> _buildScanOptions;
    private readonly ObservableCollection<RecentFolderItemViewModel> _items = [];
    private readonly ObservableCollection<RecentFolderItemViewModel> _flyoutItems = [];
    private readonly RelayCommand _clearRecentFoldersCommand;
    private readonly AsyncRelayCommand<RecentFolderItemViewModel?> _openRecentFolderCommand;
    private readonly RelayCommand<RecentFolderItemViewModel?> _removeRecentFolderCommand;

    [ObservableProperty]
    private bool hasSnapshot;

    public RecentFoldersViewModel(
        IAnalysisSessionController analysisSessionController,
        ISettingsCoordinator settingsCoordinator,
        IFolderPathService folderPathService,
        Func<ScanOptions> buildScanOptions)
    {
        _analysisSessionController = analysisSessionController;
        _settingsCoordinator = settingsCoordinator;
        _folderPathService = folderPathService;
        _buildScanOptions = buildScanOptions;

        Items = new ReadOnlyObservableCollection<RecentFolderItemViewModel>(_items);
        FlyoutItems = new ReadOnlyObservableCollection<RecentFolderItemViewModel>(_flyoutItems);

        _clearRecentFoldersCommand = new RelayCommand(ClearRecentFolders);
        _openRecentFolderCommand = new AsyncRelayCommand<RecentFolderItemViewModel?>(OpenRecentFolderAsync);
        _removeRecentFolderCommand = new RelayCommand<RecentFolderItemViewModel?>(RemoveRecentFolder);

        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _settingsCoordinator.State.RecentFolderPathsChanged += SettingsStateOnRecentFolderPathsChanged;

        HasSnapshot = _analysisSessionController.HasSnapshot;
        RefreshRecentFolders();
    }

    public ReadOnlyObservableCollection<RecentFolderItemViewModel> Items { get; }

    public ReadOnlyObservableCollection<RecentFolderItemViewModel> FlyoutItems { get; }

    public bool HasRecentFolders => Items.Count > 0;

    public bool ShowStartSurface => !HasSnapshot;

    public bool ShowEmptyState => !HasRecentFolders;

    public IRelayCommand ClearRecentFoldersCommand => _clearRecentFoldersCommand;

    public IAsyncRelayCommand<RecentFolderItemViewModel?> OpenRecentFolderCommand => _openRecentFolderCommand;

    public IRelayCommand<RecentFolderItemViewModel?> RemoveRecentFolderCommand => _removeRecentFolderCommand;

    private async Task OpenRecentFolderAsync(RecentFolderItemViewModel? folder)
    {
        if (folder is null || !folder.CanOpen)
        {
            return;
        }

        await _analysisSessionController.OpenFolderAsync(folder!.FullPath, _buildScanOptions());
    }

    private void RemoveRecentFolder(RecentFolderItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        _settingsCoordinator.RemoveRecentFolder(folder.FullPath);
    }

    private void ClearRecentFolders()
    {
        _settingsCoordinator.ClearRecentFolders();
    }

    private void SettingsStateOnRecentFolderPathsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentFolders();
        OnPropertyChanged(nameof(HasRecentFolders));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void AnalysisSessionControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnalysisSessionController.CurrentSnapshot):
                HasSnapshot = _analysisSessionController.HasSnapshot;
                break;
            case nameof(IAnalysisSessionController.State):
                HasSnapshot = _analysisSessionController.HasSnapshot;
                if (_analysisSessionController.State == AnalysisState.Completed &&
                    _analysisSessionController.CurrentSnapshot is { } completedSnapshot)
                {
                    _settingsCoordinator.RecordRecentFolder(completedSnapshot.RootPath);
                }

                break;
        }
    }

    partial void OnHasSnapshotChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStartSurface));
    }

    private void RefreshRecentFolders()
    {
        _items.Clear();
        _flyoutItems.Clear();

        foreach (var folderPath in _settingsCoordinator.State.RecentFolderPaths)
        {
            var item = CreateRecentFolderItem(folderPath);
            _items.Add(item);
            _flyoutItems.Add(item);
        }

        if (_flyoutItems.Count == 0)
        {
            _flyoutItems.Add(CreateEmptyFlyoutItem());
        }
    }

    private RecentFolderItemViewModel CreateRecentFolderItem(string folderPath)
    {
        return new RecentFolderItemViewModel(
            GetFolderDisplayName(folderPath),
            folderPath.Trim(),
            isMissing: !_folderPathService.Exists(folderPath.Trim()));
    }

    private static RecentFolderItemViewModel CreateEmptyFlyoutItem()
    {
        return new RecentFolderItemViewModel(
            "No previous folders yet",
            string.Empty,
            secondaryText: "Analyze a folder once and it will appear here.",
            canOpen: false,
            showFolderIcon: false);
    }

    private static string GetFolderDisplayName(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        var trimmedPath = folderPath.Trim();
        var displayName = Path.GetFileName(trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(displayName)
            ? trimmedPath
            : displayName;
    }
}
