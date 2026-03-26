using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.ViewModels;

internal sealed record MainWindowViewModelDependencies(
    IAnalysisSessionController AnalysisSessionController,
    TreemapNavigationState TreemapNavigationState,
    ISettingsCoordinator SettingsCoordinator,
    IFolderPathService FolderPathService,
    IPathShellService PathShellService);

internal static class MainWindowViewModelDefaults
{
    public static MainWindowViewModelDependencies Create()
    {
        var folderPathService = new NullFolderPathService();
        return new MainWindowViewModelDependencies(
            CreateAnalysisSessionController(
                new NullProjectAnalyzer(),
                new NullFolderPickerService(),
                folderPathService),
            new TreemapNavigationState(),
            new NullSettingsCoordinator(),
            folderPathService,
            new NullPathShellService());
    }

    private static AnalysisSessionController CreateAnalysisSessionController(
        IProjectAnalyzer projectAnalyzer,
        IFolderPickerService folderPickerService,
        IFolderPathService folderPathService)
    {
        ArgumentNullException.ThrowIfNull(projectAnalyzer);
        ArgumentNullException.ThrowIfNull(folderPickerService);
        ArgumentNullException.ThrowIfNull(folderPathService);

        return new AnalysisSessionController(projectAnalyzer, folderPickerService, folderPathService, logger: null, scanOptionsResolver: null);
    }

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullProjectAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Project analyzer is not configured.");
    }

    private sealed class NullFolderPathService : IFolderPathService
    {
        public bool Exists(string folderPath) => true;
    }

    private sealed class NullSettingsCoordinator : ISettingsCoordinator
    {
        public SettingsState State { get; } = new();

        public CurrentFolderSettingsState CurrentFolderState { get; } = new();

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) => baseOptions;

        public void SwitchActiveFolder(string? rootPath)
        {
        }
    }

    private sealed class NullPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
