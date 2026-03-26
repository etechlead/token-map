using System.Diagnostics;
using System.Text;
using System.Text.Json;

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
        string settingsLabel)
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
            Trace.TraceWarning($"Unable to load {settingsLabel} from '{settingsFilePath}': {exception.Message}");
            return default;
        }
    }

    public static void TrySave<TPersisted>(
        string settingsFilePath,
        TPersisted persistedSettings,
        JsonSerializerOptions serializerOptions,
        string settingsLabel)
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
            Trace.TraceWarning($"Unable to save {settingsLabel} to '{settingsFilePath}': {exception.Message}");
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
                    Trace.TraceWarning($"Unable to clean up temporary {settingsLabel} file '{tempFilePath}': {exception.Message}");
                }
            }
        }
    }
}
