using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

internal sealed class SnapshotProjectAnalyzer(ProjectSnapshot snapshot) : IProjectAnalyzer
{
    public Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(snapshot);
}

internal sealed class FixedFolderPickerService(string path) : IFolderPickerService
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(path);
}

internal sealed class ExistingFolderPathService : IFolderPathService
{
    public bool Exists(string folderPath) => true;
}

internal sealed class NoOpPathShellService : IPathShellService
{
    public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

internal sealed class InlineSettingsCoordinator(SettingsState state) : ISettingsCoordinator
{
    public SettingsState State { get; } = state;
}
