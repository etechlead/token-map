using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class JsonFolderSettingsStore : IFolderSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly PathNormalizer _pathNormalizer;
    private readonly string _folderSettingsRootPath;

    public JsonFolderSettingsStore(string? folderSettingsRootPath = null, PathNormalizer? pathNormalizer = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        _folderSettingsRootPath = string.IsNullOrWhiteSpace(folderSettingsRootPath)
            ? TokenMapAppDataPaths.GetFolderSettingsRootPath()
            : folderSettingsRootPath;
    }

    public FolderSettings Load(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var settings = FolderSettings.CreateDefault(normalizedRootPath);
        var settingsFilePath = GetSettingsFilePath(normalizedRootPath);

        if (!File.Exists(settingsFilePath))
        {
            return settings;
        }

        try
        {
            using var stream = File.OpenRead(settingsFilePath);
            var persistedSettings = JsonSerializer.Deserialize<PersistedFolderSettings>(stream, SerializerOptions);
            ApplySettings(settings, normalizedRootPath, persistedSettings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning($"Unable to load folder settings from '{settingsFilePath}': {exception.Message}");
        }

        return settings;
    }

    public void Save(string rootPath, FolderSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var settingsFilePath = GetSettingsFilePath(normalizedRootPath);
        var tempFilePath = $"{settingsFilePath}.tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
            var normalizedSettings = settings.Clone();
            normalizedSettings.RootPath = normalizedRootPath;
            normalizedSettings.Scan = FolderScanSettings.Normalize(normalizedSettings.Scan);

            var json = JsonSerializer.Serialize(normalizedSettings, SerializerOptions);
            File.WriteAllText(tempFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempFilePath, settingsFilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Unable to save folder settings to '{settingsFilePath}': {exception.Message}");
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning($"Unable to clean up temporary folder settings file '{tempFilePath}': {exception.Message}");
                }
            }
        }
    }

    private void ApplySettings(FolderSettings settings, string normalizedRootPath, PersistedFolderSettings? persistedSettings)
    {
        if (persistedSettings is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(persistedSettings.RootPath))
        {
            var persistedRootPath = _pathNormalizer.NormalizeRootPath(persistedSettings.RootPath);
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

    private string GetSettingsFilePath(string normalizedRootPath) =>
        TokenMapAppDataPaths.GetFolderSettingsFilePath(
            normalizedRootPath,
            _folderSettingsRootPath,
            _pathNormalizer);

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
