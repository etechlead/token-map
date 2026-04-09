using System.Text.Json;
using System.Text.Json.Serialization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
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
        var persistedSettings = ToPersistedSettings(normalizedSettings);

        JsonSettingsFileHelper.TrySave(
            _settingsFilePath,
            persistedSettings,
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

            if (analysis.VisibleMetricIds is { } visibleMetricIds)
            {
                settings.Analysis.VisibleMetricIds =
                [
                    .. visibleMetricIds
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => new MetricId(value!))
                ];
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

        if (persistedSettings?.Appearance?.WorkspaceLayoutMode is { } workspaceLayoutMode)
        {
            settings.Appearance.WorkspaceLayoutMode = workspaceLayoutMode;
        }

        if (persistedSettings?.Appearance?.TreemapPalette is { } treemapPalette)
        {
            settings.Appearance.TreemapPalette = treemapPalette;
        }

        if (persistedSettings?.Appearance?.ShowTreemapMetricValues is { } showTreemapMetricValues)
        {
            settings.Appearance.ShowTreemapMetricValues = showTreemapMetricValues;
        }

        if (!string.IsNullOrWhiteSpace(persistedSettings?.Localization?.ApplicationLanguageTag))
        {
            settings.Localization.ApplicationLanguageTag = persistedSettings.Localization.ApplicationLanguageTag!;
        }

        if (!string.IsNullOrWhiteSpace(persistedSettings?.Prompting?.SelectedPromptLanguageTag))
        {
            settings.Prompting.SelectedPromptLanguageTag = persistedSettings.Prompting.SelectedPromptLanguageTag!;
        }

        if (persistedSettings?.Prompting?.RefactorPromptTemplatesByLanguage is { } refactorPromptTemplatesByLanguage)
        {
            settings.Prompting.RefactorPromptTemplatesByLanguage = refactorPromptTemplatesByLanguage
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
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

    private static PersistedAppSettings ToPersistedSettings(AppSettings settings) =>
        new()
        {
            Analysis = new PersistedAnalysisSettings
            {
                SelectedMetric = settings.Analysis.SelectedMetric,
                VisibleMetricIds = [.. settings.Analysis.VisibleMetricIds.Select(metricId => metricId.Value)],
                RespectGitIgnore = settings.Analysis.RespectGitIgnore,
                UseGlobalExcludes = settings.Analysis.UseGlobalExcludes,
                GlobalExcludes = [.. settings.Analysis.GlobalExcludes],
            },
            Appearance = new PersistedAppearanceSettings
            {
                ThemePreference = settings.Appearance.ThemePreference,
                WorkspaceLayoutMode = settings.Appearance.WorkspaceLayoutMode,
                TreemapPalette = settings.Appearance.TreemapPalette,
                ShowTreemapMetricValues = settings.Appearance.ShowTreemapMetricValues,
            },
            Localization = new PersistedLocalizationSettings
            {
                ApplicationLanguageTag = settings.Localization.ApplicationLanguageTag,
            },
            Prompting = new PersistedPromptingSettings
            {
                SelectedPromptLanguageTag = settings.Prompting.SelectedPromptLanguageTag,
                RefactorPromptTemplatesByLanguage = settings.Prompting.RefactorPromptTemplatesByLanguage
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => (string?)pair.Value,
                        StringComparer.OrdinalIgnoreCase),
            },
            Logging = new PersistedLoggingSettings
            {
                MinLevel = settings.Logging.MinLevel,
            },
            RecentFolderPaths = [.. settings.RecentFolderPaths],
        };

    private static List<string> NormalizeGlobalExcludes(IEnumerable<string?> persistedEntries) =>
        [.. GlobalExcludeList.Normalize(persistedEntries.OfType<string>())];

    private sealed class PersistedAppSettings
    {
        public PersistedAnalysisSettings? Analysis { get; set; }

        public PersistedAppearanceSettings? Appearance { get; set; }

        public PersistedLocalizationSettings? Localization { get; set; }

        public PersistedPromptingSettings? Prompting { get; set; }

        public PersistedLoggingSettings? Logging { get; set; }

        public List<string>? RecentFolderPaths { get; set; }
    }

    private sealed class PersistedAnalysisSettings
    {
        [JsonConverter(typeof(NullableMetricIdConverter))]
        public MetricId? SelectedMetric { get; set; }

        public List<string>? VisibleMetricIds { get; set; }

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

        [JsonConverter(typeof(NullableStringEnumConverter<WorkspaceLayoutMode>))]
        public WorkspaceLayoutMode? WorkspaceLayoutMode { get; set; }

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

    private sealed class PersistedLocalizationSettings
    {
        public string? ApplicationLanguageTag { get; set; }
    }

    private sealed class PersistedPromptingSettings
    {
        public string? SelectedPromptLanguageTag { get; set; }

        public Dictionary<string, string?>? RefactorPromptTemplatesByLanguage { get; set; }
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

    private sealed class NullableMetricIdConverter : JsonConverter<MetricId?>
    {
        public override MetricId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String &&
                reader.GetString() is { Length: > 0 } value)
            {
                return new MetricId(value);
            }

            using var _ = JsonDocument.ParseValue(ref reader);
            return null;
        }

        public override void Write(Utf8JsonWriter writer, MetricId? value, JsonSerializerOptions options)
        {
            if (value is { } metricId)
            {
                writer.WriteStringValue(metricId.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
