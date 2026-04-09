using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.Tests.Infrastructure;

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

        Assert.Equal(MetricIds.Tokens, settings.Analysis.SelectedMetric);
        Assert.True(settings.Analysis.RespectGitIgnore);
        Assert.True(settings.Analysis.UseGlobalExcludes);
        Assert.Equal(GlobalExcludeDefaults.DefaultEntries, settings.Analysis.GlobalExcludes);
        Assert.Equal(ThemePreference.System, settings.Appearance.ThemePreference);
        Assert.Equal(WorkspaceLayoutMode.SideBySide, settings.Appearance.WorkspaceLayoutMode);
        Assert.Equal(TreemapPalette.Weighted, settings.Appearance.TreemapPalette);
        Assert.True(settings.Appearance.ShowTreemapMetricValues);
        Assert.Equal(ApplicationLanguageTags.Default, settings.Prompting.SelectedPromptLanguageTag);
        Assert.Empty(settings.Prompting.RefactorPromptTemplatesByLanguage);
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
                "selectedMetric": "non_empty_lines",
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
                "workspaceLayoutMode": "Stacked",
                "treemapPalette": "Studio",
                "showTreemapMetricValues": false
              },
              "prompting": {
                "selectedPromptLanguageTag": "ru",
                "refactorPromptTemplatesByLanguage": {
                  "en-US": "Path={{relative_path}}",
                  "ru": "Путь={{relative_path}}"
                }
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

        Assert.Equal(MetricIds.NonEmptyLines, settings.Analysis.SelectedMetric);
        Assert.False(settings.Analysis.RespectGitIgnore);
        Assert.False(settings.Analysis.UseGlobalExcludes);
        Assert.Collection(
            settings.Analysis.GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));
        Assert.Equal(ThemePreference.Dark, settings.Appearance.ThemePreference);
        Assert.Equal(WorkspaceLayoutMode.Stacked, settings.Appearance.WorkspaceLayoutMode);
        Assert.Equal(TreemapPalette.Studio, settings.Appearance.TreemapPalette);
        Assert.False(settings.Appearance.ShowTreemapMetricValues);
        Assert.Equal("ru", settings.Prompting.SelectedPromptLanguageTag);
        Assert.Equal("Path={{relative_path}}", settings.Prompting.RefactorPromptTemplatesByLanguage[ApplicationLanguageTags.Default]);
        Assert.Equal("Путь={{relative_path}}", settings.Prompting.RefactorPromptTemplatesByLanguage["ru"]);
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

        Assert.Equal(MetricIds.Tokens, settings.Analysis.SelectedMetric);
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
    public void Load_FallsBackToDefault_ForUnknownWorkspaceLayoutMode()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "appearance": {
                "workspaceLayoutMode": "Diagonal"
              }
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(WorkspaceLayoutMode.SideBySide, settings.Appearance.WorkspaceLayoutMode);
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

        Assert.Equal(MetricIds.Tokens, settings.Analysis.SelectedMetric);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == AppLogLevel.Warning &&
                     entry.EventCode == "settings.app.load_failed" &&
                     entry.Message == "Loading app settings failed." &&
                     entry.Context.TryGetValue("SettingsLabel", out var settingsLabel) &&
                     settingsLabel == "app settings");
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
        settings.Analysis.SelectedMetric = MetricIds.FileSizeBytes;
        settings.Analysis.RespectGitIgnore = false;
        settings.Analysis.UseGlobalExcludes = false;
        settings.Analysis.GlobalExcludes = [" node_modules\\ ", "", "/src//generated/**", "!nested/scripts/"];
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Appearance.WorkspaceLayoutMode = WorkspaceLayoutMode.Stacked;
        settings.Appearance.TreemapPalette = TreemapPalette.Weighted;
        settings.Appearance.ShowTreemapMetricValues = false;
        settings.Prompting.SelectedPromptLanguageTag = "ru";
        settings.Prompting.RefactorPromptTemplatesByLanguage[ApplicationLanguageTags.Default] = "Priority={{refactor_priority}}";
        settings.Prompting.RefactorPromptTemplatesByLanguage["ru"] = "Приоритет={{refactor_priority}}";
        settings.Logging.MinLevel = AppLogLevel.Warning;
        settings.RecentFolderPaths = ["C:\\RepoA", "C:\\RepoB"];

        store.Save(settings);

        var persistedJson = File.ReadAllText(GetSettingsFilePath());
        Assert.DoesNotContain("selectedTokenProfile", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("respectIgnore", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("useDefaultExcludes", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""useGlobalExcludes"": false", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""globalExcludes"": [", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""workspaceLayoutMode"": ""Stacked""", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""treemapPalette"": ""Weighted""", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""showTreemapMetricValues"": false", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""prompting"": {", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""selectedPromptLanguageTag"": ""ru""", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""refactorPromptTemplatesByLanguage"": {", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""en-US"": ""Priority={{refactor_priority}}""", persistedJson, StringComparison.Ordinal);
        Assert.Contains(@"""ru"":", persistedJson, StringComparison.Ordinal);

        var reloaded = store.Load();

        Assert.Equal(MetricIds.FileSizeBytes, reloaded.Analysis.SelectedMetric);
        Assert.False(reloaded.Analysis.RespectGitIgnore);
        Assert.False(reloaded.Analysis.UseGlobalExcludes);
        Assert.Collection(
            reloaded.Analysis.GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));
        Assert.Equal(ThemePreference.Dark, reloaded.Appearance.ThemePreference);
        Assert.Equal(WorkspaceLayoutMode.Stacked, reloaded.Appearance.WorkspaceLayoutMode);
        Assert.Equal(TreemapPalette.Weighted, reloaded.Appearance.TreemapPalette);
        Assert.False(reloaded.Appearance.ShowTreemapMetricValues);
        Assert.Equal("ru", reloaded.Prompting.SelectedPromptLanguageTag);
        Assert.Equal("Priority={{refactor_priority}}", reloaded.Prompting.RefactorPromptTemplatesByLanguage[ApplicationLanguageTags.Default]);
        Assert.Equal("Приоритет={{refactor_priority}}", reloaded.Prompting.RefactorPromptTemplatesByLanguage["ru"]);
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
        settings.Analysis.SelectedMetric = MetricIds.NonEmptyLines;

        store.Save(settings);

        var persistedJson = File.ReadAllText(GetSettingsFilePath());

        Assert.Contains(@"""selectedMetric"": ""non_empty_lines""", persistedJson, StringComparison.Ordinal);
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
        public List<AppLogEntry> Entries { get; } = [];

        public void Log(AppLogEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
