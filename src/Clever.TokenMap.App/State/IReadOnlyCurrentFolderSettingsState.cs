using System.Collections.Generic;
using System.ComponentModel;

namespace Clever.TokenMap.App.State;

public interface IReadOnlyCurrentFolderSettingsState : INotifyPropertyChanged
{
    string? ActiveRootPath { get; }

    bool UseFolderExcludes { get; }

    bool HasActiveFolder { get; }

    IReadOnlyList<string> FolderExcludes { get; }
}
