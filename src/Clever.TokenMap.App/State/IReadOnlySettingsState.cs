using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.App.State;

public interface IReadOnlySettingsState : INotifyPropertyChanged
{
    AnalysisMetric SelectedMetric { get; }

    bool RespectGitIgnore { get; }

    bool UseGlobalExcludes { get; }

    ThemePreference SelectedThemePreference { get; }

    TreemapPalette SelectedTreemapPalette { get; }

    ReadOnlyObservableCollection<string> RecentFolderPaths { get; }

    IReadOnlyList<string> GlobalExcludes { get; }

    event NotifyCollectionChangedEventHandler? RecentFolderPathsChanged;
}
