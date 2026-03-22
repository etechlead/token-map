using Avalonia.Media;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

internal static class TreemapColorRules
{
    public static Color GetLeafColor(ProjectNode node)
    {
        var groupSeed = GetParentDirectorySeed(node);
        var hash = groupSeed.Aggregate(17, (current, character) => current * 31 + character);
        var hue = Math.Abs(hash % 360);

        return ColorFromHsv(hue, saturation: 0.72, value: 0.72);
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

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/');

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

