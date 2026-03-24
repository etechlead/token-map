using Clever.TokenMap.App.Services;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public void Constructor_AppliesPersistedSettingsToStateWithoutSaving()
    {
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.NonEmptyLines;
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.UseGlobalExcludes = false;
        settings.Analysis.GlobalExcludes = ["bin/", "obj/"];
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Appearance.TreemapPalette = TreemapPalette.Weighted;

        var store = new RecordingAppSettingsStore(settings);
        var themeService = new RecordingThemeService();
        var coordinator = new SettingsCoordinator(store, new RecordingFolderSettingsStore(), themeService, debounceDelay: TimeSpan.FromMilliseconds(25));

        Assert.Equal(AnalysisMetric.TotalLines, coordinator.State.SelectedMetric);
        Assert.False(coordinator.State.RespectGitIgnore);
        Assert.False(coordinator.State.UseGlobalExcludes);
        Assert.Collection(
            coordinator.State.GlobalExcludes,
            entry => Assert.Equal("bin/", entry),
            entry => Assert.Equal("obj/", entry));
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
        var coordinator = new SettingsCoordinator(store, new RecordingFolderSettingsStore(), new RecordingThemeService(), debounceDelay: TimeSpan.FromMilliseconds(25));

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
        var coordinator = new SettingsCoordinator(store, new RecordingFolderSettingsStore(), themeService, debounceDelay: TimeSpan.FromMilliseconds(40));

        coordinator.State.SelectedMetric = AnalysisMetric.TotalLines;
        coordinator.State.SelectedMetric = AnalysisMetric.NonEmptyLines;
        coordinator.State.SelectedTreemapPalette = TreemapPalette.Weighted;
        coordinator.State.ReplaceGlobalExcludes(["bin/", "obj/"]);

        await Task.Delay(120);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(AnalysisMetric.TotalLines, store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal(TreemapPalette.Weighted, store.LastSavedSettings.Appearance.TreemapPalette);
        Assert.Collection(
            store.LastSavedSettings.Analysis.GlobalExcludes,
            entry => Assert.Equal("bin/", entry),
            entry => Assert.Equal("obj/", entry));
    }

    [Fact]
    public async Task RecentFolders_AreDebouncedIntoSingleSave()
    {
        var store = new RecordingAppSettingsStore(AppSettings.CreateDefault());
        var coordinator = new SettingsCoordinator(store, new RecordingFolderSettingsStore(), new RecordingThemeService(), debounceDelay: TimeSpan.FromMilliseconds(40));

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
        var coordinator = new SettingsCoordinator(store, new RecordingFolderSettingsStore(), themeService, debounceDelay: TimeSpan.FromSeconds(5));

        coordinator.State.SelectedMetric = AnalysisMetric.TotalLines;
        coordinator.State.SelectedThemePreference = ThemePreference.Dark;
        coordinator.State.SelectedTreemapPalette = TreemapPalette.Studio;
        coordinator.State.ReplaceGlobalExcludes(["vendor/"]);

        Assert.Equal(0, store.SaveCallCount);

        await coordinator.FlushAsync();

        Assert.Equal(1, store.SaveCallCount);
        Assert.NotNull(store.LastSavedSettings);
        Assert.Equal(AnalysisMetric.TotalLines, store.LastSavedSettings!.Analysis.SelectedMetric);
        Assert.Equal(ThemePreference.Dark, store.LastSavedSettings.Appearance.ThemePreference);
        Assert.Equal(TreemapPalette.Studio, store.LastSavedSettings.Appearance.TreemapPalette);
        Assert.Collection(
            store.LastSavedSettings.Analysis.GlobalExcludes,
            entry => Assert.Equal("vendor/", entry));
        Assert.Equal(AppLogLevel.Error, store.LastSavedSettings.Logging.MinLevel);
        Assert.Empty(store.LastSavedSettings.RecentFolderPaths);
        Assert.Equal(ThemePreference.Dark, themeService.LastAppliedThemePreference);
    }

    [Fact]
    public async Task SwitchActiveFolder_FlushesPreviousFolderAndLoadsNextFolderState()
    {
        var folderStore = new RecordingFolderSettingsStore();
        folderStore.Seed(@"C:\RepoB", new FolderSettings
        {
            RootPath = @"C:\RepoB",
            Scan = new FolderScanSettings
            {
                UseFolderExcludes = true,
                FolderExcludes = ["/generated/"],
            },
        });

        var coordinator = new SettingsCoordinator(
            new RecordingAppSettingsStore(AppSettings.CreateDefault()),
            folderStore,
            new RecordingThemeService(),
            debounceDelay: TimeSpan.FromSeconds(5));

        coordinator.SwitchActiveFolder(@"C:\RepoA");
        coordinator.CurrentFolderState.UseFolderExcludes = true;
        coordinator.CurrentFolderState.ReplaceFolderExcludes(["/dist/"]);

        coordinator.SwitchActiveFolder(@"C:\RepoB");

        Assert.Equal(1, folderStore.SaveCallCount);
        Assert.Equal(@"C:\RepoA", folderStore.LastSavedSettings?.RootPath);
        Assert.Collection(
            folderStore.LastSavedSettings!.Scan.FolderExcludes,
            entry => Assert.Equal("/dist/", entry));
        Assert.Equal(@"C:\RepoB", coordinator.CurrentFolderState.ActiveRootPath);
        Assert.True(coordinator.CurrentFolderState.UseFolderExcludes);
        Assert.Collection(
            coordinator.CurrentFolderState.FolderExcludes,
            entry => Assert.Equal("/generated/", entry));

        await coordinator.FlushAsync();
    }

    [Fact]
    public void Resolve_UsesFolderSettingsForTargetRoot()
    {
        var folderStore = new RecordingFolderSettingsStore();
        folderStore.Seed(@"C:\RepoB", new FolderSettings
        {
            RootPath = @"C:\RepoB",
            Scan = new FolderScanSettings
            {
                UseFolderExcludes = true,
                FolderExcludes = ["/generated/"],
            },
        });

        var coordinator = new SettingsCoordinator(
            new RecordingAppSettingsStore(AppSettings.CreateDefault()),
            folderStore,
            new RecordingThemeService(),
            debounceDelay: TimeSpan.FromMilliseconds(25));
        var baseOptions = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes = [".git/"],
        };

        var resolved = coordinator.Resolve(@"C:\RepoB", baseOptions);

        Assert.False(resolved.RespectGitIgnore);
        Assert.True(resolved.UseGlobalExcludes);
        Assert.True(resolved.UseFolderExcludes);
        Assert.Collection(
            resolved.FolderExcludes,
            entry => Assert.Equal("/generated/", entry));
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

    private sealed class RecordingFolderSettingsStore : IFolderSettingsStore
    {
        private readonly Dictionary<string, FolderSettings> _settingsByPath = new(StringComparer.OrdinalIgnoreCase);

        public int SaveCallCount { get; private set; }

        public FolderSettings? LastSavedSettings { get; private set; }

        public FolderSettings Load(string rootPath)
        {
            if (_settingsByPath.TryGetValue(rootPath, out var settings))
            {
                return settings.Clone();
            }

            return FolderSettings.CreateDefault(rootPath);
        }

        public void Save(string rootPath, FolderSettings settings)
        {
            SaveCallCount++;
            LastSavedSettings = settings.Clone();
            _settingsByPath[rootPath] = settings.Clone();
        }

        public void Seed(string rootPath, FolderSettings settings)
        {
            _settingsByPath[rootPath] = settings.Clone();
        }
    }
}
