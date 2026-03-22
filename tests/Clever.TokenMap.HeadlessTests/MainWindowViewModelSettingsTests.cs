using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.HeadlessTests;

public sealed class MainWindowViewModelSettingsTests
{
    [Fact]
    public void Constructor_AppliesPersistedAnalysisSettings()
    {
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = "Code lines";
        settings.Analysis.SelectedTokenProfile = "p50k_base";
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.RespectIgnore = false;
        settings.Analysis.UseDefaultExcludes = false;

        var store = new RecordingAppSettingsStore(settings);

        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(),
            new StubFolderPickerService(),
            store);

        Assert.Equal("Code lines", viewModel.Toolbar.SelectedMetric);
        Assert.Equal("p50k_base", viewModel.Toolbar.SelectedTokenProfile);
        Assert.False(viewModel.Toolbar.RespectGitIgnore);
        Assert.False(viewModel.Toolbar.RespectIgnore);
        Assert.False(viewModel.Toolbar.UseDefaultExcludes);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public void ToolbarChanges_PersistUpdatedSettings()
    {
        var settings = AppSettings.CreateDefault();
        settings.Logging.MinLevel = "Error";
        var store = new RecordingAppSettingsStore(settings);
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(),
            new StubFolderPickerService(),
            store);

        viewModel.Toolbar.SelectedMetric = "Total lines";

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal("Total lines", store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal("Error", store.LastSavedSettings.Logging.MinLevel);
    }

    private sealed class RecordingAppSettingsStore(AppSettings initialSettings) : IAppSettingsStore
    {
        public int SaveCallCount { get; private set; }

        public AppSettings? LastSavedSettings { get; private set; }

        public AppSettings Load() => Clone(initialSettings);

        public void Save(AppSettings settings)
        {
            SaveCallCount++;
            LastSavedSettings = Clone(settings);
        }

        private static AppSettings Clone(AppSettings settings) =>
            new()
            {
                Analysis = new AnalysisSettings
                {
                    SelectedMetric = settings.Analysis.SelectedMetric,
                    SelectedTokenProfile = settings.Analysis.SelectedTokenProfile,
                    RespectGitIgnore = settings.Analysis.RespectGitIgnore,
                    RespectIgnore = settings.Analysis.RespectIgnore,
                    UseDefaultExcludes = settings.Analysis.UseDefaultExcludes,
                },
                Logging = new LoggingSettings
                {
                    MinLevel = settings.Logging.MinLevel,
                },
            };
    }

    private sealed class StubFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubProjectAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
