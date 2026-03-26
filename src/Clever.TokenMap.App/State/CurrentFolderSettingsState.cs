using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.State;

public sealed partial class CurrentFolderSettingsState : ObservableObject, IReadOnlyCurrentFolderSettingsState
{
    private List<string> _folderExcludes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFolder))]
    private string? activeRootPath;

    [ObservableProperty]
    private bool useFolderExcludes;

    public bool HasActiveFolder => !string.IsNullOrWhiteSpace(ActiveRootPath);

    public IReadOnlyList<string> FolderExcludes => _folderExcludes;

    public void ReplaceFolderExcludes(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedEntries = GlobalExcludeList.Normalize(entries).ToList();
        if (_folderExcludes.SequenceEqual(normalizedEntries, StringComparer.Ordinal))
        {
            return;
        }

        _folderExcludes = normalizedEntries;
        OnPropertyChanged(nameof(FolderExcludes));
    }

    public void Load(string? rootPath, bool useFolderExcludes, IEnumerable<string> folderExcludes)
    {
        ArgumentNullException.ThrowIfNull(folderExcludes);

        ActiveRootPath = string.IsNullOrWhiteSpace(rootPath) ? null : rootPath.Trim();
        UseFolderExcludes = useFolderExcludes;
        _folderExcludes = GlobalExcludeList.Normalize(folderExcludes).ToList();
        OnPropertyChanged(nameof(FolderExcludes));
    }

    public void Reset()
    {
        ActiveRootPath = null;
        UseFolderExcludes = false;
        _folderExcludes = [];
        OnPropertyChanged(nameof(FolderExcludes));
    }
}
