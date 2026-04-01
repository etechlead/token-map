using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class SettingsState : ObservableObject, IReadOnlySettingsState
{
    private const int MaxRecentFolderCount = 10;
    private static readonly StringComparer RecentFolderComparer = PathComparison.Comparer;

    private readonly ObservableCollection<string> _recentFolderPaths = [];
    private List<string> _globalExcludes = [.. GlobalExcludeDefaults.DefaultEntries];

    public SettingsState()
    {
        RecentFolderPaths = new ReadOnlyObservableCollection<string>(_recentFolderPaths);
        _recentFolderPaths.CollectionChanged += (_, e) => RecentFolderPathsChanged?.Invoke(this, e);
    }

    [ObservableProperty]
    private MetricId selectedMetric = MetricIds.Tokens;

    [ObservableProperty]
    private bool respectGitIgnore = true;

    [ObservableProperty]
    private bool useGlobalExcludes = true;

    [ObservableProperty]
    private ThemePreference selectedThemePreference = ThemePreference.System;

    [ObservableProperty]
    private TreemapPalette selectedTreemapPalette = TreemapPalette.Weighted;

    [ObservableProperty]
    private bool showTreemapMetricValues = true;

    public ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

    public IReadOnlyList<string> GlobalExcludes => _globalExcludes;

    public event NotifyCollectionChangedEventHandler? RecentFolderPathsChanged;

    public void RecordRecentFolder(string folderPath)
    {
        if (NormalizeFolderPath(folderPath) is not { } normalizedFolderPath)
        {
            return;
        }

        var existingIndex = IndexOfRecentFolder(normalizedFolderPath);
        if (existingIndex == 0)
        {
            return;
        }

        if (existingIndex > 0)
        {
            _recentFolderPaths.RemoveAt(existingIndex);
        }

        _recentFolderPaths.Insert(0, normalizedFolderPath);
        while (_recentFolderPaths.Count > MaxRecentFolderCount)
        {
            _recentFolderPaths.RemoveAt(_recentFolderPaths.Count - 1);
        }
    }

    public void RemoveRecentFolder(string folderPath)
    {
        if (NormalizeFolderPath(folderPath) is not { } normalizedFolderPath)
        {
            return;
        }

        var existingIndex = IndexOfRecentFolder(normalizedFolderPath);
        if (existingIndex >= 0)
        {
            _recentFolderPaths.RemoveAt(existingIndex);
        }
    }

    public void ClearRecentFolders()
    {
        if (_recentFolderPaths.Count == 0)
        {
            return;
        }

        _recentFolderPaths.Clear();
    }

    public void ReplaceGlobalExcludes(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedEntries = GlobalExcludeList.Normalize(entries).ToList();
        if (_globalExcludes.SequenceEqual(normalizedEntries, StringComparer.Ordinal))
        {
            return;
        }

        _globalExcludes = normalizedEntries;
        OnPropertyChanged(nameof(GlobalExcludes));
    }

    internal void ReplaceRecentFolderPaths(IEnumerable<string> folderPaths)
    {
        ArgumentNullException.ThrowIfNull(folderPaths);

        _recentFolderPaths.Clear();
        foreach (var folderPath in AppSettingsCanonicalizer.NormalizeRecentFolderPaths(folderPaths))
        {
            _recentFolderPaths.Add(folderPath);
        }
    }

    internal void LoadGlobalExcludes(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _globalExcludes = GlobalExcludeList.Normalize(entries).ToList();
        OnPropertyChanged(nameof(GlobalExcludes));
    }

    private int IndexOfRecentFolder(string folderPath)
    {
        for (var index = 0; index < _recentFolderPaths.Count; index++)
        {
            if (RecentFolderComparer.Equals(_recentFolderPaths[index], folderPath))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? NormalizeFolderPath(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return null;
        }

        return folderPath.Trim();
    }
}
