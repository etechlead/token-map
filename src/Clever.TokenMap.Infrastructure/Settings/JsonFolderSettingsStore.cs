using System.Text.Json;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class JsonFolderSettingsStore : IFolderSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSettingsFileHelper.CreateSerializerOptions();

    private readonly PathNormalizer _pathNormalizer;
    private readonly string _folderSettingsRootPath;
    private readonly IAppLogger? _logger;

    public JsonFolderSettingsStore(
        string? folderSettingsRootPath = null,
        PathNormalizer? pathNormalizer = null,
        IAppStoragePaths? appStoragePaths = null,
        IAppLogger? logger = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        var storagePaths = appStoragePaths ?? new TokenMapAppDataPaths(pathNormalizer: _pathNormalizer);
        _folderSettingsRootPath = string.IsNullOrWhiteSpace(folderSettingsRootPath)
            ? storagePaths.GetFolderSettingsRootPath()
            : folderSettingsRootPath;
        _logger = logger;
    }

    public FolderSettings Load(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var settings = FolderSettings.CreateDefault(normalizedRootPath);
        var settingsFilePath = GetSettingsFilePath(normalizedRootPath);
        var persistedSettings = JsonSettingsFileHelper.TryLoad<PersistedFolderSettings>(
            settingsFilePath,
            SerializerOptions,
            "folder settings",
            _logger);
        ApplySettings(settings, normalizedRootPath, persistedSettings);

        return settings;
    }

    public void Save(string rootPath, FolderSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var settingsFilePath = GetSettingsFilePath(normalizedRootPath);
        var normalizedSettings = settings.Clone();
        normalizedSettings.RootPath = normalizedRootPath;
        normalizedSettings.Scan = FolderScanSettings.Normalize(normalizedSettings.Scan);

        JsonSettingsFileHelper.TrySave(
            settingsFilePath,
            normalizedSettings,
            SerializerOptions,
            "folder settings",
            _logger);
    }

    private void ApplySettings(FolderSettings settings, string normalizedRootPath, PersistedFolderSettings? persistedSettings)
    {
        if (persistedSettings is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(persistedSettings.RootPath))
        {
            string persistedRootPath;
            try
            {
                persistedRootPath = _pathNormalizer.NormalizeRootPath(persistedSettings.RootPath);
            }
            catch (Exception exception) when (IsRecoverablePathException(exception))
            {
                JsonSettingsFileHelper.LogWarning(
                    _logger,
                    exception,
                    $"Ignoring invalid persisted folder settings root path '{persistedSettings.RootPath}'.");
                return;
            }

            if (!PathComparison.Comparer.Equals(persistedRootPath, normalizedRootPath))
            {
                return;
            }

            settings.RootPath = persistedRootPath;
        }

        if (persistedSettings.Scan is { } scan)
        {
            settings.Scan.UseFolderExcludes = scan.UseFolderExcludes ?? settings.Scan.UseFolderExcludes;
            if (scan.FolderExcludes is { } folderExcludes)
            {
                settings.Scan.FolderExcludes = [.. GlobalExcludeList.Normalize(folderExcludes)];
            }
        }
    }

    private string GetSettingsFilePath(string normalizedRootPath)
    {
        var directoryName = FolderSettingsStorageKey.Build(normalizedRootPath, _pathNormalizer);
        return Path.Combine(_folderSettingsRootPath, directoryName, "settings.json");
    }

    private static bool IsRecoverablePathException(Exception exception) =>
        exception is ArgumentException
        or IOException
        or NotSupportedException
        or PathTooLongException;

    private sealed class PersistedFolderSettings
    {
        public string? RootPath { get; set; }

        public PersistedFolderScanSettings? Scan { get; set; }
    }

    private sealed class PersistedFolderScanSettings
    {
        public bool? UseFolderExcludes { get; set; }

        public List<string>? FolderExcludes { get; set; }
    }
}
