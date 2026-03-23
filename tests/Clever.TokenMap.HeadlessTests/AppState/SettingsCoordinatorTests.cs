using Clever.TokenMap.App.Services;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.HeadlessTests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public void Constructor_AppliesPersistedSettingsToStateWithoutSaving()
    {
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.NonEmptyLines;
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.UseDefaultExcludes = false;
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Appearance.TreemapPalette = TreemapPalette.Weighted;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromMilliseconds(25));

        Assert.Equal(AnalysisMetric.TotalLines, coordinator.State.SelectedMetric);
        Assert.False(coordinator.State.RespectGitIgnore);
        Assert.False(coordinator.State.UseDefaultExcludes);
        Assert.Equal(ThemePreference.Dark, coordinator.State.SelectedThemePreference);
        Assert.Equal(TreemapPalette.Weighted, coordinator.State.SelectedTreemapPalette);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
        Assert.Empty(coordinator.State.RecentFolderPaths);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public void Constructor_LoadsRecentFolderPathsIntoState()
    {
        var settings = AppSettings.CreateDefault();
        settings.RecentFolderPaths = ["C:\\RepoA", "C:\\RepoB"];

        var store = new RecordingAppSettingsStore(settings);
        var coordinator = new SettingsCoordinator(store, new RecordingThemeService(), debounceDelay: TimeSpan.FromMilliseconds(25));

        Assert.Collection(
            coordinator.State.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task ToolbarChanges_AreDebouncedIntoSingleSave()
    {
        var store = new RecordingAppSettingsStore(AppSettings.CreateDefault());
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromMilliseconds(40));

        coordinator.State.SelectedMetric = AnalysisMetric.TotalLines;
        coordinator.State.SelectedMetric = AnalysisMetric.NonEmptyLines;
        coordinator.State.SelectedTreemapPalette = TreemapPalette.Weighted;

        await Task.Delay(120);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(AnalysisMetric.TotalLines, store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal(TreemapPalette.Weighted, store.LastSavedSettings.Appearance.TreemapPalette);
    }

    [Fact]
    public async Task RecentFolders_AreDebouncedIntoSingleSave()
    {
        var store = new RecordingAppSettingsStore(AppSettings.CreateDefault());
        var coordinator = new SettingsCoordinator(store, new RecordingThemeService(), debounceDelay: TimeSpan.FromMilliseconds(40));

        coordinator.State.RecordRecentFolder("C:\\RepoA");
        coordinator.State.RecordRecentFolder("C:\\RepoB");
        coordinator.State.RecordRecentFolder("C:\\RepoA");

        await Task.Delay(120);

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Collection(
            store.LastSavedSettings!.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
    }

    [Fact]
    public async Task FlushAsync_PersistsPendingChangesImmediately()
    {
        var settings = AppSettings.CreateDefault();
        settings.Logging.MinLevel = AppLogLevel.Error;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromSeconds(5));

        coordinator.State.SelectedMetric = AnalysisMetric.TotalLines;
        coordinator.State.SelectedThemePreference = ThemePreference.Dark;
        coordinator.State.SelectedTreemapPalette = TreemapPalette.Studio;

        Assert.Equal(0, store.SaveCallCount);

        await coordinator.FlushAsync();

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal(AnalysisMetric.TotalLines, store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal(ThemePreference.Dark, store.LastSavedSettings.Appearance.ThemePreference);
        Assert.Equal(TreemapPalette.Studio, store.LastSavedSettings.Appearance.TreemapPalette);
        Assert.Equal(AppLogLevel.Error, store.LastSavedSettings.Logging.MinLevel);
        Assert.Empty(store.LastSavedSettings.RecentFolderPaths);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
    }

    private sealed class RecordingAppSettingsStore(AppSettings initialSettings) : IAppSettingsStore
    {
        public int SaveCallCount { get; private set; }

        public AppSettings? LastSavedSettings { get; private set; }

        public AppSettings Load() => initialSettings.Clone();

        public void Save(AppSettings settings)
        {
            SaveCallCount++;
            LastSavedSettings = settings.Clone();
        }
    }

    private sealed class RecordingThemeService : IThemeService
    {
        public ThemePreference? LastAppliedThemePreference { get; private set; }

        public void ApplyThemePreference(ThemePreference themePreference)
        {
            LastAppliedThemePreference = themePreference;
        }
    }
}
