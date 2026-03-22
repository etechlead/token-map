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
        settings.Analysis.SelectedMetric = "Non-empty lines";
        settings.Analysis.SelectedTokenProfile = "p50k_base";
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.RespectIgnore = false;
        settings.Analysis.UseDefaultExcludes = false;
        settings.Appearance.ThemePreference = ThemePreferences.Dark;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();

        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(),
            new StubFolderPickerService(),
            store,
            themeService);

        Assert.Equal("Non-empty lines", viewModel.Toolbar.SelectedMetric);
        Assert.Equal("p50k_base", viewModel.Toolbar.SelectedTokenProfile);
        Assert.False(viewModel.Toolbar.RespectGitIgnore);
        Assert.False(viewModel.Toolbar.RespectIgnore);
        Assert.False(viewModel.Toolbar.UseDefaultExcludes);
        Assert.Equal(ThemePreferences.Dark, viewModel.Toolbar.SelectedThemePreference);
        Assert.Equal(ThemePreferences.Dark, themeService.LastAppliedThemePreference);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public void ToolbarChanges_PersistUpdatedSettings()
    {
        var settings = AppSettings.CreateDefault();
        settings.Logging.MinLevel = "Error";
        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(),
            new StubFolderPickerService(),
            store,
            themeService);

        viewModel.Toolbar.SelectedMetric = "Total lines";
        viewModel.Toolbar.SelectedThemePreference = ThemePreferences.Dark;

        Assert.Equal(2, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal("Total lines", store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal(ThemePreferences.Dark, store.LastSavedSettings.Appearance.ThemePreference);
        Assert.Equal("Error", store.LastSavedSettings.Logging.MinLevel);
        Assert.Equal(ThemePreferences.Dark, themeService.LastAppliedThemePreference);
    }

    [Fact]
    public void Constructor_MapsLegacyCodeLinesMetricToNonEmptyLines()
    {
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = "Code lines";

        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(),
            new StubFolderPickerService(),
            new RecordingAppSettingsStore(settings),
            new RecordingThemeService());

        Assert.Equal("Non-empty lines", viewModel.Toolbar.SelectedMetric);
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
                Appearance = new AppearanceSettings
                {
                    ThemePreference = settings.Appearance.ThemePreference,
                },
                Logging = new LoggingSettings
                {
                    MinLevel = settings.Logging.MinLevel,
                },
            };
    }

    private sealed class RecordingThemeService : IThemeService
    {
        public string CurrentSystemTheme => ThemePreferences.Light;

        public string? LastAppliedThemePreference { get; private set; }

        public void ApplyThemePreference(string themePreference)
        {
            LastAppliedThemePreference = themePreference;
        }
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
