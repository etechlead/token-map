using Clever.TokenMap.Infrastructure.Settings;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Infrastructure;

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
        var rootPath = TestPaths.Folder("Repo");

        var settings = store.Load(rootPath);

        Assert.Equal(rootPath, settings.RootPath);
        Assert.False(settings.Scan.UseFolderExcludes);
        Assert.Empty(settings.Scan.FolderExcludes);
    }

    [Fact]
    public void Load_FallsBackToDefaults_WhenFileIsMalformed()
    {
        var store = CreateStore();
        var rootPath = TestPaths.Folder("Repo");
        var settingsFilePath = GetSettingsFilePath(rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        File.WriteAllText(settingsFilePath, "{ invalid json");

        var settings = store.Load(rootPath);

        Assert.Equal(rootPath, settings.RootPath);
        Assert.False(settings.Scan.UseFolderExcludes);
        Assert.Empty(settings.Scan.FolderExcludes);
    }

    [Fact]
    public void Load_IgnoresInvalidPersistedRootPath()
    {
        var store = CreateStore();
        var rootPath = TestPaths.Folder("Repo");
        var settingsFilePath = GetSettingsFilePath(rootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        File.WriteAllText(
            settingsFilePath,
            """
            {
              "rootPath": "C:\\Repo\u0000Broken",
              "scan": {
                "useFolderExcludes": true,
                "folderExcludes": ["/dist/"]
              }
            }
            """);

        var settings = store.Load(rootPath);

        Assert.Equal(rootPath, settings.RootPath);
        Assert.False(settings.Scan.UseFolderExcludes);
        Assert.Empty(settings.Scan.FolderExcludes);
    }

    [Fact]
    public void Save_WritesJsonThatCanBeLoadedAgain()
    {
        var store = CreateStore();
        var rootPath = TestPaths.Folder("Repo");
        var settings = FolderSettings.CreateDefault(rootPath);
        settings.Scan.UseFolderExcludes = true;
        settings.Scan.FolderExcludes = [" /dist// ", "", "# generated", "!/dist/keep.txt"];

        store.Save(rootPath, settings);

        var reloaded = store.Load(rootPath);

        Assert.Equal(rootPath, reloaded.RootPath);
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

    private JsonFolderSettingsStore CreateStore() => new(
        new TokenMapAppDataPaths(_testRootPath).GetFolderSettingsRootPath());

    private string GetSettingsFilePath(string rootPath)
    {
        var storagePaths = new TokenMapAppDataPaths(_testRootPath);
        var normalizedRootPath = new Clever.TokenMap.Core.Paths.PathNormalizer().NormalizeRootPath(rootPath);
        var directoryName = FolderSettingsStorageKey.Build(normalizedRootPath);
        return Path.Combine(storagePaths.GetFolderSettingsRootPath(), directoryName, "settings.json");
    }
}
