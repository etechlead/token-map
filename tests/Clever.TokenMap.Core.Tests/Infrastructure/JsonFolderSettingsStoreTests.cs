using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class JsonFolderSettingsStoreTests : IDisposable
{
    private readonly string _testRootPath = Path.Combine(
        Path.GetTempPath(),
        "TokenMap.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildKey_IsStableReadableAndBounded()
    {
        var rootPath = @"C:\Work\Company\Product\Frontend\App\Nested\Project";

        var key = FolderSettingsStorageKey.Build(rootPath);

        Assert.True(key.Length <= 64);
        Assert.Matches("^[a-z0-9-]+-[a-f0-9]{12}$", key);
        Assert.Equal(key, FolderSettingsStorageKey.Build(rootPath));
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var store = CreateStore();

        var settings = store.Load(@"C:\Repo");

        Assert.Equal(@"C:\Repo", settings.RootPath);
        Assert.False(settings.Scan.UseFolderExcludes);
        Assert.Empty(settings.Scan.FolderExcludes);
    }

    [Fact]
    public void Load_FallsBackToDefaults_WhenFileIsMalformed()
    {
        var store = CreateStore();
        var settingsFilePath = TokenMapAppDataPaths.GetFolderSettingsFilePath(@"C:\Repo", _testRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        File.WriteAllText(settingsFilePath, "{ invalid json");

        var settings = store.Load(@"C:\Repo");

        Assert.Equal(@"C:\Repo", settings.RootPath);
        Assert.False(settings.Scan.UseFolderExcludes);
        Assert.Empty(settings.Scan.FolderExcludes);
    }

    [Fact]
    public void Save_WritesJsonThatCanBeLoadedAgain()
    {
        var store = CreateStore();
        var settings = FolderSettings.CreateDefault(@"C:\Repo");
        settings.Scan.UseFolderExcludes = true;
        settings.Scan.FolderExcludes = [" /dist// ", "", "# generated", "!/dist/keep.txt"];

        store.Save(@"C:\Repo", settings);

        var reloaded = store.Load(@"C:\Repo");

        Assert.Equal(@"C:\Repo", reloaded.RootPath);
        Assert.True(reloaded.Scan.UseFolderExcludes);
        Assert.Collection(
            reloaded.Scan.FolderExcludes,
            entry => Assert.Equal("/dist/", entry),
            entry => Assert.Equal("# generated", entry),
            entry => Assert.Equal("!/dist/keep.txt", entry));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    private JsonFolderSettingsStore CreateStore() => new(_testRootPath);
}
