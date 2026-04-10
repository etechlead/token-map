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
    private static readonly StringComparer PromptTemplateLanguageComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ObservableCollection<string> _recentFolderPaths = [];
    private List<MetricId> _visibleMetricIds = [.. DefaultMetricCatalog.GetDefaultVisibleMetricIds()];
    private List<string> _globalExcludes = [.. GlobalExcludeDefaults.DefaultEntries];
    private Dictionary<string, string> _refactorPromptTemplatesByLanguage = new(PromptTemplateLanguageComparer);

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
    private WorkspaceLayoutMode workspaceLayoutMode = WorkspaceLayoutMode.SideBySide;

    [ObservableProperty]
    private TreemapPalette selectedTreemapPalette = TreemapPalette.Weighted;

    [ObservableProperty]
    private bool showTreemapMetricValues = true;

    [ObservableProperty]
    private string applicationLanguageTag = ApplicationLanguageTags.System;

    [ObservableProperty]
    private string selectedPromptLanguageTag = ApplicationLanguageTags.Default;

    public ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

    public IReadOnlyList<MetricId> VisibleMetricIds => _visibleMetricIds;

    public IReadOnlyList<string> GlobalExcludes => _globalExcludes;

    public IReadOnlyDictionary<string, string> RefactorPromptTemplatesByLanguage => _refactorPromptTemplatesByLanguage;

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

    public void SetMetricVisibility(MetricId metricId, bool isVisible)
    {
        var normalizedMetricId = DefaultMetricCatalog.NormalizeMetricId(metricId);
        if (isVisible)
        {
            if (_visibleMetricIds.Contains(normalizedMetricId))
            {
                return;
            }

            ReplaceVisibleMetricIdsCore([.. _visibleMetricIds, normalizedMetricId]);
            return;
        }

        if (!_visibleMetricIds.Contains(normalizedMetricId) || _visibleMetricIds.Count <= 1)
        {
            return;
        }

        ReplaceVisibleMetricIdsCore(_visibleMetricIds.Where(id => id != normalizedMetricId));
    }

    public string GetRefactorPromptTemplate(string languageTag)
    {
        var normalizedLanguageTag = AppSettingsCanonicalizer.NormalizePromptLanguageTag(languageTag);
        if (normalizedLanguageTag is null)
        {
            return string.Empty;
        }

        return _refactorPromptTemplatesByLanguage.TryGetValue(normalizedLanguageTag, out var template)
            ? template
            : string.Empty;
    }

    public void SetRefactorPromptTemplate(string languageTag, string templateText)
    {
        var normalizedLanguageTag = AppSettingsCanonicalizer.NormalizePromptLanguageTag(languageTag);
        if (normalizedLanguageTag is null)
        {
            return;
        }

        var normalizedTemplate = AppSettingsCanonicalizer.NormalizeRefactorPromptTemplatesByLanguage(
            new Dictionary<string, string>
            {
                [normalizedLanguageTag] = templateText,
            });
        var nextTemplate = normalizedTemplate.GetValueOrDefault(normalizedLanguageTag, string.Empty);
        if (_refactorPromptTemplatesByLanguage.TryGetValue(normalizedLanguageTag, out var currentTemplate) &&
            string.Equals(currentTemplate, nextTemplate, StringComparison.Ordinal))
        {
            return;
        }

        _refactorPromptTemplatesByLanguage[normalizedLanguageTag] = nextTemplate;
        OnPropertyChanged(nameof(RefactorPromptTemplatesByLanguage));
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

    internal void ReplaceVisibleMetricIds(IEnumerable<MetricId> metricIds)
    {
        ArgumentNullException.ThrowIfNull(metricIds);
        ReplaceVisibleMetricIdsCore(metricIds);
    }

    internal void ReplaceRefactorPromptTemplatesByLanguage(IReadOnlyDictionary<string, string> templatesByLanguage)
    {
        ArgumentNullException.ThrowIfNull(templatesByLanguage);

        var normalizedTemplates = AppSettingsCanonicalizer.NormalizeRefactorPromptTemplatesByLanguage(templatesByLanguage);
        if (_refactorPromptTemplatesByLanguage.Count == normalizedTemplates.Count &&
            _refactorPromptTemplatesByLanguage.All(pair =>
                normalizedTemplates.TryGetValue(pair.Key, out var value) &&
                string.Equals(pair.Value, value, StringComparison.Ordinal)))
        {
            return;
        }

        _refactorPromptTemplatesByLanguage = new Dictionary<string, string>(normalizedTemplates, PromptTemplateLanguageComparer);
        OnPropertyChanged(nameof(RefactorPromptTemplatesByLanguage));
    }

    private void ReplaceVisibleMetricIdsCore(IEnumerable<MetricId> metricIds)
    {
        var normalizedMetricIds = AppSettingsCanonicalizer.NormalizeVisibleMetricIds(metricIds);
        if (_visibleMetricIds.SequenceEqual(normalizedMetricIds))
        {
            return;
        }

        _visibleMetricIds = normalizedMetricIds;
        if (!_visibleMetricIds.Contains(SelectedMetric))
        {
            SelectedMetric = _visibleMetricIds[0];
        }

        OnPropertyChanged(nameof(VisibleMetricIds));
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
