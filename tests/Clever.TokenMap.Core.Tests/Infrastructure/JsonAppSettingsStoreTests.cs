using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Enums;

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
        Assert.True(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreference.System, settings.Appearance.ThemePreference);
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
                "selectedMetric": "NonEmptyLines",
                "selectedTokenProfile": 123,
                "respectGitIgnore": false,
                "useDefaultExcludes": false
              },
              "appearance": {
                "themePreference": "Dark"
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

        Assert.Equal(AnalysisMetric.TotalLines, settings.Analysis.SelectedMetric);
        Assert.False(settings.Analysis.RespectGitIgnore);
        Assert.False(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreference.Dark, settings.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Error, settings.Logging.MinLevel);
        Assert.Collection(
            settings.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
    }

    [Fact]
    public void Load_FallsBackToDefaults_ForLegacyAliasValues()
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
        settings.Analysis.SelectedMetric = AnalysisMetric.TotalLines;
        settings.Analysis.RespectGitIgnore = false;
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Logging.MinLevel = AppLogLevel.Warning;
        settings.RecentFolderPaths = ["C:\\RepoA", "C:\\RepoB"];

        store.Save(settings);

        var persistedJson = File.ReadAllText(GetSettingsFilePath());
        Assert.DoesNotContain("selectedTokenProfile", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("respectIgnore", persistedJson, StringComparison.Ordinal);

        var reloaded = store.Load();

        Assert.Equal(AnalysisMetric.TotalLines, reloaded.Analysis.SelectedMetric);
        Assert.False(reloaded.Analysis.RespectGitIgnore);
        Assert.Equal(ThemePreference.Dark, reloaded.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Warning, reloaded.Logging.MinLevel);
        Assert.Collection(
            reloaded.RecentFolderPaths,
            path => Assert.Equal("C:\\RepoA", path),
            path => Assert.Equal("C:\\RepoB", path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    private JsonAppSettingsStore CreateStore() => new(GetSettingsFilePath());

    private string GetSettingsFilePath() => Path.Combine(_testRootPath, "settings.json");
}
