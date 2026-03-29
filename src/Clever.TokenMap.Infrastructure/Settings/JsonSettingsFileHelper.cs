using System.Text;
using System.Text.Json;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Logging;

namespace Clever.TokenMap.Infrastructure.Settings;

internal static class JsonSettingsFileHelper
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static JsonSerializerOptions CreateSerializerOptions(Action<JsonSerializerOptions>? configure = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        configure?.Invoke(options);
        return options;
    }

    public static TPersisted? TryLoad<TPersisted>(
        string settingsFilePath,
        JsonSerializerOptions serializerOptions,
        string settingsLabel,
        string issueCodePrefix,
        IAppLogger? logger = null)
    {
        if (!File.Exists(settingsFilePath))
        {
            return default;
        }

        try
        {
            using var stream = File.OpenRead(settingsFilePath);
            return JsonSerializer.Deserialize<TPersisted>(stream, serializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Log(
                logger,
                AppLogLevel.Warning,
                exception,
                $"Loading {settingsLabel} failed.",
                eventCode: $"{issueCodePrefix}.load_failed",
                context: AppIssueContext.Create(
                    ("SettingsLabel", settingsLabel),
                    ("SettingsFilePath", settingsFilePath)));
            return default;
        }
    }

    public static void TrySave<TPersisted>(
        string settingsFilePath,
        TPersisted persistedSettings,
        JsonSerializerOptions serializerOptions,
        string settingsLabel,
        string issueCodePrefix,
        IAppLogger? logger = null)
    {
        var tempFilePath = $"{settingsFilePath}.tmp";

        try
        {
            var directoryPath = Path.GetDirectoryName(settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(persistedSettings, serializerOptions);
            File.WriteAllText(tempFilePath, json, Utf8WithoutBom);
            File.Move(tempFilePath, settingsFilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log(
                logger,
                AppLogLevel.Error,
                exception,
                $"Saving {settingsLabel} failed.",
                eventCode: $"{issueCodePrefix}.save_failed",
                context: AppIssueContext.Create(
                    ("SettingsLabel", settingsLabel),
                    ("SettingsFilePath", settingsFilePath),
                    ("TempFilePath", tempFilePath)));
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
                    LogWarning(
                        logger,
                        exception,
                        $"Cleaning up the temporary {settingsLabel} file failed.",
                        eventCode: $"{issueCodePrefix}.temp_cleanup_failed",
                        context: AppIssueContext.Create(
                            ("SettingsLabel", settingsLabel),
                            ("SettingsFilePath", settingsFilePath),
                            ("TempFilePath", tempFilePath)));
                }
            }
        }
    }

    internal static void LogWarning(
        IAppLogger? logger,
        Exception exception,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null)
        => Log(logger, AppLogLevel.Warning, exception, message, eventCode, context);

    private static void Log(
        IAppLogger? logger,
        AppLogLevel level,
        Exception exception,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null)
    {
        logger?.Log(new AppLogEntry
        {
            Level = level,
            Message = message,
            EventCode = eventCode,
            Exception = exception,
            Context = context ?? AppIssueContext.Empty,
        });
    }

}
