using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _testRootPath = Path.Combine(
        Path.GetTempPath(),
        "TokenMap.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(AnalysisMetric.Tokens, settings.Analysis.SelectedMetric);
        Assert.True(settings.Analysis.RespectGitIgnore);
        Assert.True(settings.Analysis.UseGlobalExcludes);
        Assert.Equal(GlobalExcludeDefaults.DefaultEntries, settings.Analysis.GlobalExcludes);
        Assert.Equal(ThemePreference.System, settings.Appearance.ThemePreference);
        Assert.Equal(TreemapPalette.Weighted, settings.Appearance.TreemapPalette);
        Assert.Empty(settings.RecentFolderPaths);
    }

    [Fact]
    public void Load_MergesRecognizedValues_AndKeepsDefaultsForInvalidFields()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "analysis": {
                "selectedMetric": "Lines",
                "selectedTokenProfile": 123,
                "respectGitIgnore": false,
                "useGlobalExcludes": false,
                "globalExcludes": [
                  " node_modules\\\\ ",
                  "",
                  "/src//generated/**",
                  "!nested/scripts/"
                ]
              },
              "appearance": {
                "themePreference": "Dark",
                "treemapPalette": "Studio"
              },
              "logging": {
                "minLevel": "Error"
              },
              "recentFolderPaths": [
                "C:\\RepoA",
                "",
                "C:\\RepoA",
                "C:\\RepoB"
              ]
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(AnalysisMetric.Lines, settings.Analysis.SelectedMetric);
        Assert.False(settings.Analysis.RespectGitIgnore);
        Assert.False(settings.Analysis.UseGlobalExcludes);
        Assert.Collection(
            settings.Analysis.GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));
        Assert.Equal(ThemePreference.Dark, settings.Appearance.ThemePreference);
        Assert.Equal(TreemapPalette.Studio, settings.Appearance.TreemapPalette);
        Assert.Equal(AppLogLevel.Error, settings.Logging.MinLevel);
        Assert.Collection(
            settings.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
    }

    [Fact]
    public void Load_FallsBackToDefaults_ForUnknownLineMetricAliasValues()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "analysis": {
                "selectedMetric": "Non-empty lines",
                "selectedTokenProfile": "p50k_base"
              },
              "appearance": {
                "themePreference": "Light"
              }
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(AnalysisMetric.Tokens, settings.Analysis.SelectedMetric);
        Assert.Equal(ThemePreference.Light, settings.Appearance.ThemePreference);
    }

    [Fact]
    public void Load_FallsBackToDefault_ForUnknownTreemapPalette()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "appearance": {
                "treemapPalette": "Emphasis"
              }
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(TreemapPalette.Weighted, settings.Appearance.TreemapPalette);
    }

    [Fact]
    public void Load_IgnoresLegacyUseDefaultExcludes_WhenCanonicalFlagIsAbsent()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "analysis": {
                "useDefaultExcludes": false
              }
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.True(settings.Analysis.UseGlobalExcludes);
    }

    [Fact]
    public void Load_LogsWarning_WhenSettingsJsonIsMalformed()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(GetSettingsFilePath(), "{ invalid json");
        var logger = new RecordingLogger();
        var store = CreateStore(logger);

        var settings = store.Load();

        Assert.Equal(AnalysisMetric.Tokens, settings.Analysis.SelectedMetric);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == AppLogLevel.Warning &&
                     entry.Message.Contains("Unable to load app settings", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_UsesCurrentPlatformPathComparer_ForRecentFolders()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "recentFolderPaths": [
                "/Users/demo/Repo",
                "/Users/demo/repo"
              ]
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        if (OperatingSystem.IsWindows())
        {
            Assert.Collection(
                settings.RecentFolderPaths,
                path => Assert.Equal("/Users/demo/Repo", path));
            return;
        }

        Assert.Collection(
            settings.RecentFolderPaths,
            path => Assert.Equal("/Users/demo/Repo", path),
            path => Assert.Equal("/Users/demo/repo", path));
    }

    [Fact]
    public void Save_WritesJsonThatCanBeLoadedAgain()
    {
        var store = CreateStore();
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.Size;
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.UseGlobalExcludes = false;
        settings.Analysis.GlobalExcludes = [" node_modules\\ ", "", "/src//generated/**", "!nested/scripts/"];
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Appearance.TreemapPalette = TreemapPalette.Weighted;
        settings.Logging.MinLevel = AppLogLevel.Warning;
        settings.RecentFolderPaths = ["C:\\RepoA", "C:\\RepoB"];

        store.Save(settings);

        var persistedJson = File.ReadAllText(GetSettingsFilePath());
        Assert.DoesNotContain("selectedTokenProfile", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("respectIgnore", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("useDefaultExcludes", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""useGlobalExcludes"": false", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""globalExcludes"": [", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""treemapPalette"": ""Weighted""", persistedJson, StringComparison.Ordinal);

        var reloaded = store.Load();

        Assert.Equal(AnalysisMetric.Size, reloaded.Analysis.SelectedMetric);
        Assert.False(reloaded.Analysis.RespectGitIgnore);
        Assert.False(reloaded.Analysis.UseGlobalExcludes);
        Assert.Collection(
            reloaded.Analysis.GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));
        Assert.Equal(ThemePreference.Dark, reloaded.Appearance.ThemePreference);
        Assert.Equal(TreemapPalette.Weighted, reloaded.Appearance.TreemapPalette);
        Assert.Equal(AppLogLevel.Warning, reloaded.Logging.MinLevel);
        Assert.Collection(
            reloaded.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
    }

    [Fact]
    public void Save_PersistsCanonicalLinesMetricName()
    {
        var store = CreateStore();
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.Lines;

        store.Save(settings);

        var persistedJson = File.ReadAllText(GetSettingsFilePath());

        Assert.Contains(@"""selectedMetric"": ""Lines""", persistedJson, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    private JsonAppSettingsStore CreateStore(IAppLogger? logger = null) => new(GetSettingsFilePath(), logger: logger);

    private string GetSettingsFilePath() => Path.Combine(_testRootPath, "settings.json");

    private sealed class RecordingLogger : IAppLogger
    {
        public List<(AppLogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public void Log(AppLogLevel level, string message, Exception? exception = null)
        {
            Entries.Add((level, message, exception));
        }
    }
}
