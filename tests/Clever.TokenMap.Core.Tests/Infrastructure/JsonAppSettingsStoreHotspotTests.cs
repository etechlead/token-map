using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class JsonAppSettingsStoreHotspotTests : IDisposable
{
    private readonly string _testRootPath = Path.Combine(
        Path.GetTempPath(),
        "TokenMap.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefaults_WhenFileContainsMalformedJson()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(
            GetSettingsFilePath(),
            """
            {
              "analysis": {
                "selectedMetric": "Tokens",
            """);

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
    public void Save_CleansUpTemporaryFile_WhenDestinationCannotBeReplaced()
    {
        Directory.CreateDirectory(_testRootPath);
        File.WriteAllText(GetSettingsFilePath(), """{"existing":true}""");
        var originalContents = File.ReadAllText(GetSettingsFilePath());
        var tempFilePath = $"{GetSettingsFilePath()}.tmp";

        using (new FileStream(
                   GetSettingsFilePath(),
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.ReadWrite))
        {
            var store = CreateStore();
            var settings = AppSettings.CreateDefault();
            settings.Analysis.SelectedMetric = AnalysisMetric.Size;
            settings.Analysis.GlobalExcludes = [" node_modules\\ ", "/src//generated/**"];

            store.Save(settings);

            Assert.False(File.Exists(tempFilePath));
        }

        Assert.Equal(originalContents, File.ReadAllText(GetSettingsFilePath()));
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
