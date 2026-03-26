using System.Globalization;
using Clever.TokenMap.Core.Enums;

internal static class CliParsing
{
    private static readonly TreemapPalette[] OrderedPalettes =
    [
        TreemapPalette.Weighted,
        TreemapPalette.Studio,
        TreemapPalette.Plain,
    ];

    public static string? GetOptionValue(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    public static bool HasFlag(string[] args, string optionName) =>
        args.Any(argument => string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase));

    public static string GetRequiredOptionValue(string[] args, string optionName) =>
        GetOptionValue(args, optionName)
        ?? throw new InvalidOperationException($"Missing required option: {optionName}");

    public static ThemePreference ParseThemePreference(string value) =>
        value.ToLowerInvariant() switch
        {
            "light" => ThemePreference.Light,
            "dark" => ThemePreference.Dark,
            "system" => ThemePreference.System,
            _ => throw new InvalidOperationException($"Unsupported theme '{value}'. Expected light, dark, or system."),
        };

    public static AnalysisMetric ParseMetric(string value) =>
        value.ToLowerInvariant() switch
        {
            "tokens" => AnalysisMetric.Tokens,
            "lines" => AnalysisMetric.Lines,
            "size" => AnalysisMetric.Size,
            _ => throw new InvalidOperationException($"Unsupported metric '{value}'. Expected tokens, lines, or size."),
        };

    public static CaptureSource ParseCaptureSource(string value) =>
        value.ToLowerInvariant() switch
        {
            "repo" => CaptureSource.Repo,
            "demo" => CaptureSource.Demo,
            _ => throw new InvalidOperationException($"Unsupported source '{value}'. Expected repo or demo."),
        };

    public static IReadOnlyList<CaptureSurface> ParseCaptureSurfaces(string value)
    {
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.GetValues<CaptureSurface>();
        }

        var surfaces = SplitList(value)
            .Select(ParseCaptureSurface)
            .Distinct()
            .ToArray();
        if (surfaces.Length == 0)
        {
            throw new InvalidOperationException("At least one capture surface must be specified.");
        }

        return surfaces;
    }

    public static IReadOnlyList<TreemapPalette> ParsePalettes(string value)
    {
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return OrderedPalettes;
        }

        var palettes = SplitList(value)
            .Select(ParsePalette)
            .Distinct()
            .ToArray();
        if (palettes.Length == 0)
        {
            throw new InvalidOperationException("At least one treemap palette must be specified.");
        }

        return palettes;
    }

    public static string GetDefaultArtifactDirectory(string folderName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", folderName, timestamp));
    }

    public static int GetOptionalIntValue(string[] args, string optionName, int fallback)
    {
        var rawValue = GetOptionValue(args, optionName);
        return rawValue is null
            ? fallback
            : int.Parse(rawValue, CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static CaptureSurface ParseCaptureSurface(string value) =>
        value.ToLowerInvariant() switch
        {
            "main" => CaptureSurface.Main,
            "settings" => CaptureSurface.Settings,
            "treemap" => CaptureSurface.Treemap,
            _ => throw new InvalidOperationException($"Unsupported surface '{value}'. Expected main, settings, treemap, or all."),
        };

    private static TreemapPalette ParsePalette(string value) =>
        value.ToLowerInvariant() switch
        {
            "plain" => TreemapPalette.Plain,
            "weighted" => TreemapPalette.Weighted,
            "studio" => TreemapPalette.Studio,
            _ => throw new InvalidOperationException($"Unsupported palette '{value}'. Expected plain, weighted, studio, or all."),
        };
}
