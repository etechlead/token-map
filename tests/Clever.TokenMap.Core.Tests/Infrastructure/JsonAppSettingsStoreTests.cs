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

        Assert.Equal("Tokens", settings.Analysis.SelectedMetric);
        Assert.Equal("o200k_base", settings.Analysis.SelectedTokenProfile);
        Assert.True(settings.Analysis.RespectGitIgnore);
        Assert.True(settings.Analysis.RespectIgnore);
        Assert.True(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreferences.System, settings.Appearance.ThemePreference);
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
                "selectedMetric": "Code lines",
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

        Assert.Equal("Code lines", settings.Analysis.SelectedMetric);
        Assert.Equal("o200k_base", settings.Analysis.SelectedTokenProfile);
        Assert.False(settings.Analysis.RespectGitIgnore);
        Assert.False(settings.Analysis.RespectIgnore);
        Assert.False(settings.Analysis.UseDefaultExcludes);
        Assert.Equal(ThemePreferences.Dark, settings.Appearance.ThemePreference);
        Assert.Equal("Error", settings.Logging.MinLevel);
    }

    [Fact]
    public void Save_WritesJsonThatCanBeLoadedAgain()
    {
        var store = CreateStore();
        var settings = AppSettings.CreateDefault();
        settings.Analysis.SelectedMetric = "Total lines";
        settings.Analysis.SelectedTokenProfile = "p50k_base";
        settings.Analysis.RespectGitIgnore = false;
        settings.Appearance.ThemePreference = ThemePreferences.Dark;
        settings.Logging.MinLevel = "Warning";

        store.Save(settings);

        var reloaded = store.Load();

        Assert.Equal("Total lines", reloaded.Analysis.SelectedMetric);
        Assert.Equal("p50k_base", reloaded.Analysis.SelectedTokenProfile);
        Assert.False(reloaded.Analysis.RespectGitIgnore);
        Assert.Equal(ThemePreferences.Dark, reloaded.Appearance.ThemePreference);
        Assert.Equal("Warning", reloaded.Logging.MinLevel);
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
