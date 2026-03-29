using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private const string IssueCodePrefix = "settings.app";

    private readonly string _settingsFilePath;
    private readonly IAppLogger? _logger;

    public JsonAppSettingsStore(
        string? settingsFilePath = null,
        IAppStoragePaths? appStoragePaths = null,
        IAppLogger? logger = null)
    {
        var storagePaths = appStoragePaths ?? new TokenMapAppDataPaths();
        _settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
            ? storagePaths.GetSettingsFilePath()
            : settingsFilePath;
        _logger = logger;
    }

    public AppSettings Load()
    {
        var settings = AppSettings.CreateDefault();
        var persistedSettings = JsonSettingsFileHelper.TryLoad<PersistedAppSettings>(
            _settingsFilePath,
            SerializerOptions,
            "app settings",
            IssueCodePrefix,
            _logger);
        ApplySettings(settings, persistedSettings);

        return settings;
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedSettings = AppSettingsCanonicalizer.Normalize(settings.Clone());

        JsonSettingsFileHelper.TrySave(
            _settingsFilePath,
            normalizedSettings,
            SerializerOptions,
            "app settings",
            IssueCodePrefix,
            _logger);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = JsonSettingsFileHelper.CreateSerializerOptions();
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

            if (analysis.UseGlobalExcludes is { } useGlobalExcludes)
            {
                settings.Analysis.UseGlobalExcludes = useGlobalExcludes;
            }

            if (analysis.GlobalExcludes is { } globalExcludes)
            {
                settings.Analysis.GlobalExcludes = NormalizeGlobalExcludes(globalExcludes);
            }
        }

        if (persistedSettings?.Appearance?.ThemePreference is { } themePreference)
        {
            settings.Appearance.ThemePreference = themePreference;
        }

        if (persistedSettings?.Appearance?.TreemapPalette is { } treemapPalette)
        {
            settings.Appearance.TreemapPalette = treemapPalette;
        }

        if (persistedSettings?.Appearance?.ShowTreemapMetricValues is { } showTreemapMetricValues)
        {
            settings.Appearance.ShowTreemapMetricValues = showTreemapMetricValues;
        }

        if (persistedSettings?.Logging?.MinLevel is { } minimumLevel)
        {
            settings.Logging.MinLevel = minimumLevel;
        }

        if (persistedSettings?.RecentFolderPaths is { } recentFolderPaths)
        {
            settings.RecentFolderPaths = [.. recentFolderPaths];
        }

        AppSettingsCanonicalizer.Normalize(settings);
    }

    private static List<string> NormalizeGlobalExcludes(IEnumerable<string?> persistedEntries) =>
        [.. GlobalExcludeList.Normalize(persistedEntries.OfType<string>())];

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
        public bool? UseGlobalExcludes { get; set; }

        public List<string>? GlobalExcludes { get; set; }
    }

    private sealed class PersistedAppearanceSettings
    {
        [JsonConverter(typeof(NullableStringEnumConverter<ThemePreference>))]
        public ThemePreference? ThemePreference { get; set; }

        [JsonConverter(typeof(NullableStringEnumConverter<TreemapPalette>))]
        public TreemapPalette? TreemapPalette { get; set; }

        [JsonConverter(typeof(NullableBooleanConverter))]
        public bool? ShowTreemapMetricValues { get; set; }
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
