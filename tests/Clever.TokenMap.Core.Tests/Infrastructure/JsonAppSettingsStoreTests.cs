using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;

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
        Assert.Equal(TokenProfile.O200KBase, settings.Analysis.SelectedTokenProfile);
        Assert.True(settings.Analysis.RespectGitIgnore);
        Assert.True(settings.Analysis.RespectIgnore);
        Assert.True(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreference.System, settings.Appearance.ThemePreference);
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
                "respectIgnore": false,
                "useDefaultExcludes": false
              },
              "appearance": {
                "themePreference": "Dark"
              },
              "logging": {
                "minLevel": "Error"
              }
            }
            """);

        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(AnalysisMetric.NonEmptyLines, settings.Analysis.SelectedMetric);
        Assert.Equal(TokenProfile.O200KBase, settings.Analysis.SelectedTokenProfile);
        Assert.False(settings.Analysis.RespectGitIgnore);
        Assert.False(settings.Analysis.RespectIgnore);
        Assert.False(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreference.Dark, settings.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Error, settings.Logging.MinLevel);
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
        Assert.Equal(TokenProfile.O200KBase, settings.Analysis.SelectedTokenProfile);
        Assert.Equal(ThemePreference.Light, settings.Appearance.ThemePreference);
    }

    [Fact]
    public void Save_WritesJsonThatCanBeLoadedAgain()
    {
        var store = CreateStore();
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = AnalysisMetric.TotalLines;
        settings.Analysis.SelectedTokenProfile = TokenProfile.P50KBase;
        settings.Analysis.RespectGitIgnore = false;
        settings.Appearance.ThemePreference = ThemePreference.Dark;
        settings.Logging.MinLevel = AppLogLevel.Warning;

        store.Save(settings);

        var reloaded = store.Load();

        Assert.Equal(AnalysisMetric.TotalLines, reloaded.Analysis.SelectedMetric);
        Assert.Equal(TokenProfile.P50KBase, reloaded.Analysis.SelectedTokenProfile);
        Assert.False(reloaded.Analysis.RespectGitIgnore);
        Assert.Equal(ThemePreference.Dark, reloaded.Appearance.ThemePreference);
        Assert.Equal(AppLogLevel.Warning, reloaded.Logging.MinLevel);
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
