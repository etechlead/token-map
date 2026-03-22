using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Clever.TokenMap.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class SettingsState : ObservableObject
{
    private const int MaxRecentFolderCount = 10;
    private static readonly StringComparer RecentFolderComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly ObservableCollection<string> _recentFolderPaths = [];

    public SettingsState()
    {
        RecentFolderPaths = new ReadOnlyObservableCollection<string>(_recentFolderPaths);
        _recentFolderPaths.CollectionChanged += (_, e) => RecentFolderPathsChanged?.Invoke(this, e);
    }

    [ObservableProperty]
    private AnalysisMetric selectedMetric = AnalysisMetric.Tokens;

    [ObservableProperty]
    private TokenProfile selectedTokenProfile = TokenProfile.O200KBase;

    [ObservableProperty]
    private bool respectGitIgnore = true;

    [ObservableProperty]
    private bool respectIgnore = true;

    [ObservableProperty]
    private bool useDefaultExcludes = true;

    [ObservableProperty]
    private ThemePreference selectedThemePreference = ThemePreference.System;

    public ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

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

    internal void ReplaceRecentFolderPaths(IEnumerable<string> folderPaths)
    {
        ArgumentNullException.ThrowIfNull(folderPaths);

        _recentFolderPaths.Clear();
        foreach (var folderPath in NormalizeRecentFolderPaths(folderPaths))
        {
            _recentFolderPaths.Add(folderPath);
        }
    }

    private static List<string> NormalizeRecentFolderPaths(IEnumerable<string> folderPaths)
    {
        var uniquePaths = new HashSet<string>(RecentFolderComparer);
        var normalizedPaths = new List<string>();

        foreach (var folderPath in folderPaths)
        {
            if (NormalizeFolderPath(folderPath) is not { } normalizedFolderPath)
            {
                continue;
            }

            if (!uniquePaths.Add(normalizedFolderPath))
            {
                continue;
            }

            normalizedPaths.Add(normalizedFolderPath);

            if (normalizedPaths.Count >= MaxRecentFolderCount)
            {
                break;
            }
        }

        return normalizedPaths;
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
