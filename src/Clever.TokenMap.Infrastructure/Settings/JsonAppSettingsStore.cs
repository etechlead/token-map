using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

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
            var persistedSettings = JsonSerializer.Deserialize<PersistedAppSettings>(stream, SerializerOptions);
            ApplySettings(settings, persistedSettings);
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

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void ApplySettings(AppSettings settings, PersistedAppSettings? persistedSettings)
    {
        if (persistedSettings?.Analysis is { } analysis)
        {
            if (analysis.SelectedMetric is { } selectedMetric)
            {
                settings.Analysis.SelectedMetric = selectedMetric;
            }

            if (analysis.RespectGitIgnore is { } respectGitIgnore)
            {
                settings.Analysis.RespectGitIgnore = respectGitIgnore;
            }

            if (analysis.UseDefaultExcludes is { } useDefaultExcludes)
            {
                settings.Analysis.UseDefaultExcludes = useDefaultExcludes;
            }
        }

        if (persistedSettings?.Appearance?.ThemePreference is { } themePreference)
        {
            settings.Appearance.ThemePreference = themePreference;
        }

        if (persistedSettings?.Logging?.MinLevel is { } minimumLevel)
        {
            settings.Logging.MinLevel = minimumLevel;
        }

        if (persistedSettings?.RecentFolderPaths is { } recentFolderPaths)
        {
            settings.RecentFolderPaths = NormalizeRecentFolderPaths(recentFolderPaths);
        }
    }

    private static string GetDefaultSettingsFilePath()
        => TokenMapAppDataPaths.GetSettingsFilePath();

    private static List<string> NormalizeRecentFolderPaths(IEnumerable<string?> persistedPaths)
    {
        var uniquePaths = new HashSet<string>(PathComparison.Comparer);
        var normalizedPaths = new List<string>();

        foreach (var persistedPath in persistedPaths)
        {
            if (string.IsNullOrWhiteSpace(persistedPath))
            {
                continue;
            }

            var trimmedPath = persistedPath.Trim();
            if (!uniquePaths.Add(trimmedPath))
            {
                continue;
            }

            normalizedPaths.Add(trimmedPath);

            if (normalizedPaths.Count >= 10)
            {
                break;
            }
        }

        return normalizedPaths;
    }

    private sealed class PersistedAppSettings
    {
        public PersistedAnalysisSettings? Analysis { get; set; }

        public PersistedAppearanceSettings? Appearance { get; set; }

        public PersistedLoggingSettings? Logging { get; set; }

        public List<string>? RecentFolderPaths { get; set; }
    }

    private sealed class PersistedAnalysisSettings
    {
        [JsonConverter(typeof(NullableStringEnumConverter<AnalysisMetric>))]
        public AnalysisMetric? SelectedMetric { get; set; }

        [JsonConverter(typeof(NullableBooleanConverter))]
        public bool? RespectGitIgnore { get; set; }

        [JsonConverter(typeof(NullableBooleanConverter))]
        public bool? UseDefaultExcludes { get; set; }
    }

    private sealed class PersistedAppearanceSettings
    {
        [JsonConverter(typeof(NullableStringEnumConverter<ThemePreference>))]
        public ThemePreference? ThemePreference { get; set; }
    }

    private sealed class PersistedLoggingSettings
    {
        [JsonConverter(typeof(NullableStringEnumConverter<AppLogLevel>))]
        public AppLogLevel? MinLevel { get; set; }
    }

    private sealed class NullableBooleanConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Null => null,
                _ => ReadInvalidBoolean(ref reader),
            };
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value is { } booleanValue)
            {
                writer.WriteBooleanValue(booleanValue);
                return;
            }

            writer.WriteNullValue();
        }

        private static bool? ReadInvalidBoolean(ref Utf8JsonReader reader)
        {
            using var _ = JsonDocument.ParseValue(ref reader);
            return null;
        }
    }

    private sealed class NullableStringEnumConverter<TEnum> : JsonConverter<TEnum?>
        where TEnum : struct, Enum
    {
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String &&
                reader.GetString() is { Length: > 0 } text &&
                Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsedValue) &&
                Enum.IsDefined(parsedValue))
            {
                return parsedValue;
            }

            if (reader.TokenType == JsonTokenType.Number &&
                reader.TryGetInt32(out var rawValue) &&
                Enum.IsDefined(typeof(TEnum), rawValue))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), rawValue);
            }

            using var _ = JsonDocument.ParseValue(ref reader);
            return null;
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (value is { } enumValue)
            {
                writer.WriteStringValue(enumValue.ToString());
                return;
            }

            writer.WriteNullValue();
        }
    }
}
