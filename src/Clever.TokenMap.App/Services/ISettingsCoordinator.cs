using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface ISettingsCoordinator : IScanOptionsResolver
{
    SettingsState State { get; }

    CurrentFolderSettingsState CurrentFolderState { get; }

    ScanOptions BuildCurrentScanOptions();

    void SetSelectedMetric(AnalysisMetric metric);

    void SetRespectGitIgnore(bool value);

    void SetUseGlobalExcludes(bool value);

    void ReplaceGlobalExcludes(IEnumerable<string> entries);

    void SetThemePreference(ThemePreference preference);

    void SetTreemapPalette(TreemapPalette palette);

    void RecordRecentFolder(string folderPath);

    void RemoveRecentFolder(string folderPath);

    void ClearRecentFolders();

    void SetUseFolderExcludes(bool value);

    void ReplaceFolderExcludes(IEnumerable<string> entries);

    void SwitchActiveFolder(string? rootPath);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
