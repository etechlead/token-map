using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.VisualHarness;

internal static class CliParsing
{
    public static string? GetOptionValue(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1 || LooksLikeOptionName(args[index + 1]))
                {
                    throw new InvalidOperationException($"Missing value for option: {optionName}");
                }

                return args[index + 1];
            }
        }

        return null;
    }

    public static bool HasFlag(string[] args, string optionName) =>
        args.Any(argument => string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<CaptureSurface> ParseCaptureSurfaces(string value)
    {
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.GetValues<CaptureSurface>();
        }

        var surfaces = SplitList(value)
            .Select(ParseEnumToken<CaptureSurface>)
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
            return Enum.GetValues<TreemapPalette>();
        }

        var palettes = SplitList(value)
            .Select(ParseEnumToken<TreemapPalette>)
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
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", folderName, timestamp));
    }

    public static int ParseInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    public static long ParseLong(string value) => long.Parse(value, CultureInfo.InvariantCulture);

    public static double ParseDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    public static IReadOnlyList<string> GetMetricTokens() =>
    [
        "tokens",
        "lines",
        "size",
        "refactor",
    ];

    public static string GetMetricToken(MetricId metricId)
    {
        var normalizedMetricId = DefaultMetricCatalog.NormalizeMetricId(metricId);
        return normalizedMetricId == MetricIds.NonEmptyLines
            ? "lines"
            : normalizedMetricId == MetricIds.FileSizeBytes
                ? "size"
                : normalizedMetricId == MetricIds.RefactorPriorityPoints
                    ? "refactor"
                    : "tokens";
    }

    public static MetricId ParseMetricId(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "tokens" => MetricIds.Tokens,
            "lines" => MetricIds.NonEmptyLines,
            "size" => MetricIds.FileSizeBytes,
            "refactor" => MetricIds.RefactorPriorityPoints,
            "non_empty_lines" => MetricIds.NonEmptyLines,
            "file_size_bytes" => MetricIds.FileSizeBytes,
            "refactor_priority_points" => MetricIds.RefactorPriorityPoints,
            _ => throw new InvalidOperationException(
                $"Unsupported metric '{value}'. Expected {string.Join(", ", GetMetricTokens())}."),
        };
    }

    public static IReadOnlyList<string> GetEnumTokens<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum =>
        values.Select(GetEnumToken).ToArray();

    public static string GetEnumToken<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString(), BindingFlags.Public | BindingFlags.Static).Single();
        return member.GetCustomAttribute<DisplayAttribute>()?.GetName()
            ?? value.ToString().ToLowerInvariant();
    }

    public static TEnum ParseEnumToken<TEnum>(string value)
        where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(GetEnumToken(candidate), value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Unsupported {typeof(TEnum).Name} '{value}'. Expected {string.Join(", ", GetEnumTokens(Enum.GetValues<TEnum>()))}.");
    }

    private static string[] SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool LooksLikeOptionName(string value) =>
        value.StartsWith('-');
}
