using Avalonia;
using Avalonia.Media;

namespace Clever.TokenMap.Treemap;

internal enum TreemapInteractionState
{
    None,
    Hovered,
    Selected,
}

internal static class TreemapInteractionVisuals
{
    private const double HoverOverlayAmount = 0.08;
    private const double SelectedOverlayAmount = 0.16;
    private const double StripeInset = 4;
    private const double StripeSpacing = 8;
    private const double StripeThickness = 1;
    private const double StripeMinWidth = 24;
    private const double StripeMinHeight = 20;

    public static double GetAccentThickness(TreemapInteractionState state) =>
        state switch
        {
            TreemapInteractionState.Hovered => 1d,
            TreemapInteractionState.Selected => 2d,
            _ => 0d,
        };

    public static Color GetFillColor(Color baseColor, TreemapInteractionState state)
    {
        var overlayAmount = GetOverlayAmount(state);
        if (overlayAmount <= 0)
        {
            return baseColor;
        }

        var overlayColor = TreemapColorRules.ShouldUseDarkLeafLabel(baseColor)
            ? Color.FromRgb(0x18, 0x21, 0x2B)
            : Colors.White;
        return BlendColors(baseColor, overlayColor, overlayAmount);
    }

    public static Color GetContrastBorderColor(Color fillColor) =>
        TreemapColorRules.ShouldUseDarkLeafLabel(fillColor)
            ? Color.FromArgb(0xD6, 0x18, 0x21, 0x2B)
            : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF);

    public static bool ShouldDrawStripeOverlay(Rect bounds, TreemapInteractionState state) =>
        state == TreemapInteractionState.Selected &&
        bounds.Width >= StripeMinWidth &&
        bounds.Height >= StripeMinHeight;

    public static Rect GetStripeBounds(Rect bounds, double accentThickness)
    {
        var inset = Math.Max(StripeInset, accentThickness + 2d);
        return TreemapVisualRules.Inset(bounds, inset);
    }

    public static double GetStripeSpacing() => StripeSpacing;

    public static double GetStripeThickness() => StripeThickness;

    public static Color GetStripeColor(Color fillColor)
    {
        var contrastColor = GetContrastBorderColor(fillColor);
        var alpha = TreemapColorRules.ShouldUseDarkLeafLabel(fillColor)
            ? (byte)0x38
            : (byte)0x50;
        return Color.FromArgb(alpha, contrastColor.R, contrastColor.G, contrastColor.B);
    }

    private static double GetOverlayAmount(TreemapInteractionState state) =>
        state switch
        {
            TreemapInteractionState.Hovered => HoverOverlayAmount,
            TreemapInteractionState.Selected => SelectedOverlayAmount,
            _ => 0d,
        };

    private static Color BlendColors(Color from, Color to, double amount)
    {
        var blend = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (byte)Math.Round(Lerp(from.A, to.A, blend)),
            (byte)Math.Round(Lerp(from.R, to.R, blend)),
            (byte)Math.Round(Lerp(from.G, to.G, blend)),
            (byte)Math.Round(Lerp(from.B, to.B, blend)));
    }

    private static double Lerp(double from, double to, double amount) =>
        from + ((to - from) * amount);
}
