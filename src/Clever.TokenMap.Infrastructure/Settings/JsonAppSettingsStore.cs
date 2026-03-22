using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    public JsonAppSettingsStore(string? settingsFilePath = null)
    {
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? GetDefaultSettingsFilePath()
            : settingsFilePath;
    }

    public AppSettings Load()
    {
        var settings = AppSettings.CreateDefault();
        if (!File.Exists(_settingsFilePath))
        {
            return settings;
        }

        try
        {
            using var stream = File.OpenRead(_settingsFilePath);
            using var document = JsonDocument.Parse(stream);
            ApplySettings(settings, document.RootElement);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning($"Unable to load app settings from '{_settingsFilePath}': {exception.Message}");
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var tempFilePath = $"{_settingsFilePath}.tmp";

        try
        {
            var directoryPath = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(tempFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Unable to save app settings to '{_settingsFilePath}': {exception.Message}");
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
                    Trace.TraceWarning($"Unable to clean up temporary app settings file '{tempFilePath}': {exception.Message}");
                }
            }
        }
    }

    private static void ApplySettings(AppSettings settings, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetObject(root, "analysis", out var analysis))
        {
            if (TryGetString(analysis, "selectedMetric", out var selectedMetric))
            {
                settings.Analysis.SelectedMetric = selectedMetric;
            }

            if (TryGetString(analysis, "selectedTokenProfile", out var selectedTokenProfile))
            {
                settings.Analysis.SelectedTokenProfile = selectedTokenProfile;
            }

            if (TryGetBoolean(analysis, "respectGitIgnore", out var respectGitIgnore))
            {
                settings.Analysis.RespectGitIgnore = respectGitIgnore;
            }

            if (TryGetBoolean(analysis, "respectIgnore", out var respectIgnore))
            {
                settings.Analysis.RespectIgnore = respectIgnore;
            }

            if (TryGetBoolean(analysis, "useDefaultExcludes", out var useDefaultExcludes))
            {
                settings.Analysis.UseDefaultExcludes = useDefaultExcludes;
            }
        }

        if (TryGetObject(root, "logging", out var logging) &&
            TryGetString(logging, "minLevel", out var minLevel) &&
            TryNormalizeMinLevel(minLevel, out var normalizedMinLevel))
        {
            settings.Logging.MinLevel = normalizedMinLevel;
        }
    }

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement parent, string propertyName, out string value)
    {
        if (parent.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            property.GetString() is { } text &&
            !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetBoolean(JsonElement parent, string propertyName, out bool value)
    {
        if (parent.TryGetProperty(propertyName, out var property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            value = property.GetBoolean();
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryNormalizeMinLevel(string value, out string normalized)
    {
        if (string.Equals(value, "Trace", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Trace";
            return true;
        }

        if (string.Equals(value, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Debug";
            return true;
        }

        if (string.Equals(value, "Information", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Information";
            return true;
        }

        if (string.Equals(value, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Warning";
            return true;
        }

        if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Error";
            return true;
        }

        if (string.Equals(value, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Critical";
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static string GetDefaultSettingsFilePath()
        => TokenMapAppDataPaths.GetSettingsFilePath();
}
