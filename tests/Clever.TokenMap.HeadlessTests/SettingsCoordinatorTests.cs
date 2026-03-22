using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.HeadlessTests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public void Attach_AppliesPersistedSettingsWithoutSaving()
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
        var toolbar = CreateToolbar();

        coordinator.Attach(toolbar);

        Assert.Equal(AnalysisMetric.NonEmptyLines, toolbar.SelectedMetric);
        Assert.Equal(TokenProfile.P50KBase, toolbar.SelectedTokenProfile);
        Assert.False(toolbar.RespectGitIgnore);
        Assert.False(toolbar.RespectIgnore);
        Assert.False(toolbar.UseDefaultExcludes);
        Assert.Equal(ThemePreference.Dark, toolbar.SelectedThemePreference);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task ToolbarChanges_AreDebouncedIntoSingleSave()
    {
        var store = new RecordingAppSettingsStore(AppSettings.CreateDefault());
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, themeService, debounceDelay: TimeSpan.FromMilliseconds(40));
        var toolbar = CreateToolbar();

        coordinator.Attach(toolbar);
        toolbar.SelectedMetric = AnalysisMetric.TotalLines;
        toolbar.SelectedMetric = AnalysisMetric.NonEmptyLines;

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
        var toolbar = CreateToolbar();

        coordinator.Attach(toolbar);
        toolbar.SelectedTokenProfile = TokenProfile.P50KBase;
        toolbar.SelectedThemePreference = ThemePreference.Dark;

        Assert.Equal(0, store.SaveCallCount);

        await coordinator.FlushAsync();

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal(TokenProfile.P50KBase, store.LastSavedSettings!.Analysis.SelectedTokenProfile);
        Assert.Equal(ThemePreference.Dark, store.LastSavedSettings.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Error, store.LastSavedSettings.Logging.MinLevel);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
    }

    private static ToolbarViewModel CreateToolbar() =>
        new(
            new AsyncRelayCommand(() => Task.CompletedTask),
            new AsyncRelayCommand(() => Task.CompletedTask),
            new RelayCommand(() => { }));

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
        public ThemePreference CurrentSystemTheme => ThemePreference.Light;

        public ThemePreference? LastAppliedThemePreference { get; private set; }

        public void ApplyThemePreference(ThemePreference themePreference)
        {
            LastAppliedThemePreference = themePreference;
        }
    }
}
