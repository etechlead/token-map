using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.State;

public interface IReadOnlySettingsState : INotifyPropertyChanged
{
    MetricId SelectedMetric { get; }

    IReadOnlyList<MetricId> VisibleMetricIds { get; }

    bool RespectGitIgnore { get; }

    bool UseGlobalExcludes { get; }

    ThemePreference SelectedThemePreference { get; }

    WorkspaceLayoutMode WorkspaceLayoutMode { get; }

    TreemapPalette SelectedTreemapPalette { get; }

    bool ShowTreemapMetricValues { get; }

    string ApplicationLanguageTag { get; }

    string SelectedPromptLanguageTag { get; }

    IReadOnlyDictionary<string, string> RefactorPromptTemplatesByLanguage { get; }

    ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

    IReadOnlyList<string> GlobalExcludes { get; }

    event NotifyCollectionChangedEventHandler? RecentFolderPathsChanged;

    string GetRefactorPromptTemplate(string languageTag);
}
