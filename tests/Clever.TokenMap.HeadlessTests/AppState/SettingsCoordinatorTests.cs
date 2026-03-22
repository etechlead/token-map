using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.HeadlessTests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public void Constructor_AppliesPersistedSettingsToStateWithoutSaving()
    {
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.NonEmptyLines;
        settings.Analysis.SelectedTokenProfile = TokenProfile.P50KBase;
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.RespectIgnore = false;
        settings.Analysis.UseDefaultExcludes = false;
        settings.Appearance.ThemePreference = ThemePreference.Dark;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromMilliseconds(25));

        Assert.Equal(AnalysisMetric.NonEmptyLines, coordinator.State.SelectedMetric);
        Assert.Equal(TokenProfile.P50KBase, coordinator.State.SelectedTokenProfile);
        Assert.False(coordinator.State.RespectGitIgnore);
        Assert.False(coordinator.State.RespectIgnore);
        Assert.False(coordinator.State.UseDefaultExcludes);
        Assert.Equal(ThemePreference.Dark, coordinator.State.SelectedThemePreference);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
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

        await Task.Delay(120);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(AnalysisMetric.NonEmptyLines, store.LastSavedSettings!.Analysis.SelectedMetric);
    }

    [Fact]
    public async Task FlushAsync_PersistsPendingChangesImmediately()
    {
        var settings = AppSettings.CreateDefault();
        settings.Logging.MinLevel = AppLogLevel.Error;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromSeconds(5));

        coordinator.State.SelectedTokenProfile = TokenProfile.P50KBase;
        coordinator.State.SelectedThemePreference = ThemePreference.Dark;

        Assert.Equal(0, store.SaveCallCount);

        await coordinator.FlushAsync();

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal(TokenProfile.P50KBase, store.LastSavedSettings!.Analysis.SelectedTokenProfile);
        Assert.Equal(ThemePreference.Dark, store.LastSavedSettings.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Error, store.LastSavedSettings.Logging.MinLevel);
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
