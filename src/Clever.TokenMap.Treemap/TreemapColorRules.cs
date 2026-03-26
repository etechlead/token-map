using Avalonia.Media;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

internal readonly record struct TreemapPaletteContext(
    AnalysisMetric Metric,
    long MinLeafWeight,
    long MaxLeafWeight);

internal readonly record struct StudioPaletteFamily(
    double Hue,
    double MutedHueShift,
    double MutedSaturation,
    double MutedValue,
    double StrongSaturation,
    double StrongValue);

internal static class TreemapColorRules
{
    private static readonly StudioPaletteFamily[] StudioPaletteFamilies =
    [
        new(46, -2, 0.20, 0.50, 0.72, 0.84),
        new(38, 0, 0.18, 0.48, 0.68, 0.81),
        new(30, 2, 0.19, 0.48, 0.70, 0.79),
        new(22, -2, 0.18, 0.47, 0.66, 0.78),
        new(14, -4, 0.17, 0.46, 0.60, 0.76),
        new(72, -6, 0.18, 0.48, 0.52, 0.78),
        new(84, -6, 0.18, 0.49, 0.48, 0.76),
        new(96, -4, 0.16, 0.49, 0.42, 0.78),
        new(112, -2, 0.14, 0.47, 0.36, 0.76),
        new(148, 2, 0.14, 0.48, 0.34, 0.79),
        new(162, 4, 0.15, 0.49, 0.36, 0.80),
        new(176, 6, 0.16, 0.49, 0.40, 0.80),
        new(192, 8, 0.16, 0.49, 0.38, 0.82),
        new(206, 6, 0.16, 0.48, 0.36, 0.81),
        new(220, 4, 0.17, 0.48, 0.40, 0.78),
        new(334, -2, 0.15, 0.46, 0.34, 0.74),
    ];

    public static TreemapPaletteContext CreatePaletteContext(IEnumerable<ProjectNode> leafNodes, AnalysisMetric metric)
    {
        var minLeafWeight = long.MaxValue;
        var maxLeafWeight = 0L;

        foreach (var node in leafNodes)
        {
            var weight = GetMetricValue(node, metric);
            if (weight <= 0)
            {
                continue;
            }

            minLeafWeight = Math.Min(minLeafWeight, weight);
            maxLeafWeight = Math.Max(maxLeafWeight, weight);
        }

        if (maxLeafWeight <= 0)
        {
            return new TreemapPaletteContext(metric, 0, 0);
        }

        return new TreemapPaletteContext(
            metric,
            minLeafWeight == long.MaxValue ? maxLeafWeight : minLeafWeight,
            maxLeafWeight);
    }

    public static Color GetLeafColor(ProjectNode node, TreemapPalette palette, TreemapPaletteContext context)
    {
        return palette switch
        {
            TreemapPalette.Plain => GetPlainLeafColor(node),
            TreemapPalette.Weighted => GetWeightedLeafColor(node, context),
            TreemapPalette.Studio => GetStudioLeafColor(node, context),
            _ => GetPlainLeafColor(node),
        };
    }

    public static bool ShouldUseDarkLeafLabel(Color fillColor)
    {
        var perceivedBrightness = (fillColor.R * 299) + (fillColor.G * 587) + (fillColor.B * 114);
        return perceivedBrightness >= 160_000;
    }

    public static string GetParentDirectorySeed(ProjectNode node)
    {
        var relativePath = NormalizePath(node.RelativePath);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "(root)";
        }

        var separatorIndex = relativePath.LastIndexOf('/');
        return separatorIndex <= 0
            ? "(root)"
            : relativePath[..separatorIndex];
    }

    private static Color GetPlainLeafColor(ProjectNode node)
    {
        var groupSeed = GetParentDirectorySeed(node);
        var hue = GetStableHash(groupSeed) % 360;

        return ColorFromHsv(hue, saturation: 0.72, value: 0.72);
    }

    private static Color GetWeightedLeafColor(ProjectNode node, TreemapPaletteContext context)
    {
        var groupSeed = GetParentDirectorySeed(node);
        var hue = GetStableHash(groupSeed) % 360;
        var normalizedWeight = GetNormalizedWeight(node, context);
        var emphasis = GetWeightEmphasis(normalizedWeight, exponent: 1.55);
        var saturation = Lerp(0.10, 0.82, emphasis);
        var value = Lerp(0.38, 0.90, emphasis);

        return ColorFromHsv(hue, saturation, value);
    }

    private static Color GetStudioLeafColor(ProjectNode node, TreemapPaletteContext context)
    {
        var groupSeed = GetParentDirectorySeed(node);
        var hash = GetStableHash(groupSeed);
        var family = GetStudioPaletteFamily(hash);
        var normalizedWeight = GetNormalizedWeight(node, context);
        var emphasis = GetWeightEmphasis(normalizedWeight, exponent: 1.20);
        var highlight = Math.Pow(Math.Clamp(normalizedWeight, 0d, 1d), 2.40) * 0.38;
        var hueShift = GetVariantOffset(hash, divisor: 11, range: 5.0);
        var saturationOffset = GetVariantOffset(hash / 11, divisor: 7, range: 0.045);
        var valueOffset = GetVariantOffset(hash / 77, divisor: 7, range: 0.035);

        var mutedColor = ColorFromHsv(
            WrapHue(family.Hue + family.MutedHueShift + (hueShift * 0.45)),
            saturation: Clamp01(family.MutedSaturation + (saturationOffset * 0.45)),
            value: Clamp01(family.MutedValue + (valueOffset * 0.35)));
        var strongColor = ColorFromHsv(
            WrapHue(family.Hue + hueShift),
            Clamp01(family.StrongSaturation + saturationOffset),
            Clamp01(family.StrongValue + valueOffset));
        var highlightColor = ColorFromHsv(
            WrapHue(family.Hue + hueShift + 4),
            Math.Min(0.88, Clamp01(family.StrongSaturation + saturationOffset + 0.08)),
            Math.Min(0.92, Clamp01(family.StrongValue + valueOffset + 0.06)));

        var baseColor = BlendColors(mutedColor, strongColor, emphasis);
        return BlendColors(baseColor, highlightColor, highlight);
    }

    private static StudioPaletteFamily GetStudioPaletteFamily(int hash) =>
        StudioPaletteFamilies[hash % StudioPaletteFamilies.Length];

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/');

    private static double GetNormalizedWeight(ProjectNode node, TreemapPaletteContext context)
    {
        if (context.MaxLeafWeight <= 0)
        {
            return 0.5;
        }

        var nodeValue = GetMetricValue(node, context.Metric);
        if (nodeValue <= 0)
        {
            return 0;
        }

        if (context.MinLeafWeight >= context.MaxLeafWeight)
        {
            return 1d;
        }

        var minWeight = Math.Log10(context.MinLeafWeight + 1d);
        var maxWeight = Math.Log10(context.MaxLeafWeight + 1d);
        var currentWeight = Math.Log10(nodeValue + 1d);

        return Math.Clamp((currentWeight - minWeight) / (maxWeight - minWeight), 0d, 1d);
    }

    private static long GetMetricValue(ProjectNode node, AnalysisMetric metric) =>
        metric.GetValue(node.Metrics);

    private static int GetStableHash(string seed)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in seed)
            {
                hash = (hash * 31) + character;
            }

            return hash & int.MaxValue;
        }
    }

    private static double GetWeightEmphasis(double normalizedWeight, double exponent) =>
        Math.Pow(SmoothStep(Math.Clamp(normalizedWeight, 0d, 1d)), exponent);

    private static double SmoothStep(double amount) =>
        amount * amount * (3d - (2d * amount));

    private static double Lerp(double from, double to, double amount) =>
        from + ((to - from) * amount);

    private static double GetVariantOffset(int value, int divisor, double range)
    {
        var bucket = Math.Abs(value % divisor);
        var centered = bucket - ((divisor - 1) / 2d);
        return centered * (range / Math.Max(1d, (divisor - 1) / 2d));
    }

    private static double Clamp01(double value) =>
        Math.Clamp(value, 0d, 1d);

    private static double WrapHue(double hue)
    {
        var wrappedHue = hue % 360d;
        return wrappedHue < 0d
            ? wrappedHue + 360d
            : wrappedHue;
    }

    private static Color BlendColors(Color from, Color to, double amount)
    {
        var blend = Math.Clamp(amount, 0d, 1d);
        return Color.FromRgb(
            (byte)Math.Round(Lerp(from.R, to.R, blend)),
            (byte)Math.Round(Lerp(from.G, to.G, blend)),
            (byte)Math.Round(Lerp(from.B, to.B, blend)));
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var section = hue / 60d;
        var x = chroma * (1 - Math.Abs(section % 2 - 1));
        var m = value - chroma;

        (double r, double g, double b) = section switch
        {
            >= 0 and < 1 => (chroma, x, 0d),
            >= 1 and < 2 => (x, chroma, 0d),
            >= 2 and < 3 => (0d, chroma, x),
            >= 3 and < 4 => (0d, x, chroma),
            >= 4 and < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
