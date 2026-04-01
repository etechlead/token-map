using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.App.Services;

public interface ISettingsCoordinator : IScanOptionsResolver
{
    IReadOnlySettingsState State { get; }

    IReadOnlyCurrentFolderSettingsState CurrentFolderState { get; }

    ScanOptions BuildCurrentScanOptions();

    void SetSelectedMetric(MetricId metric);

    void SetRespectGitIgnore(bool value);

    void SetUseGlobalExcludes(bool value);

    void ReplaceGlobalExcludes(IEnumerable<string> entries);

    void SetThemePreference(ThemePreference preference);

    void SetTreemapPalette(TreemapPalette palette);

    void SetShowTreemapMetricValues(bool value);

    void RecordRecentFolder(string folderPath);

    void RemoveRecentFolder(string folderPath);

    void ClearRecentFolders();

    void SetUseFolderExcludes(bool value);

    void ReplaceFolderExcludes(IEnumerable<string> entries);

    void SwitchActiveFolder(string? rootPath);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
