using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.App.State;

public interface IReadOnlySettingsState : INotifyPropertyChanged
{
    MetricId SelectedMetric { get; }

    bool RespectGitIgnore { get; }

    bool UseGlobalExcludes { get; }

    ThemePreference SelectedThemePreference { get; }

    TreemapPalette SelectedTreemapPalette { get; }

    bool ShowTreemapMetricValues { get; }

    ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

    IReadOnlyList<string> GlobalExcludes { get; }

    event NotifyCollectionChangedEventHandler? RecentFolderPathsChanged;
}
