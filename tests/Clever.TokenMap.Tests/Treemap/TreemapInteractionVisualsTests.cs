using Avalonia;
using Avalonia.Media;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.Tests.Treemap;

public sealed class TreemapInteractionVisualsTests
{
    [Fact]
    public void GetFillColor_Hovered_BrightensDarkTiles()
    {
        var baseColor = Color.FromRgb(0x2C, 0x58, 0x8A);

        var hovered = TreemapInteractionVisuals.GetFillColor(baseColor, TreemapInteractionState.Hovered);

        Assert.True(GetBrightness(hovered) > GetBrightness(baseColor));
    }

    [Fact]
    public void GetFillColor_Selected_DarkensLightTilesMoreThanHover()
    {
        var baseColor = Color.FromRgb(0xD4, 0xE6, 0xF7);

        var hovered = TreemapInteractionVisuals.GetFillColor(baseColor, TreemapInteractionState.Hovered);
        var selected = TreemapInteractionVisuals.GetFillColor(baseColor, TreemapInteractionState.Selected);

        Assert.True(GetBrightness(hovered) < GetBrightness(baseColor));
        Assert.True(GetBrightness(selected) < GetBrightness(hovered));
    }

    [Fact]
    public void GetContrastBorderColor_UsesDarkStrokeForLightTiles_AndLightStrokeForDarkTiles()
    {
        var lightFill = Color.FromRgb(0xE2, 0xEA, 0xF2);
        var darkFill = Color.FromRgb(0x28, 0x34, 0x42);

        var lightFillBorder = TreemapInteractionVisuals.GetContrastBorderColor(lightFill);
        var darkFillBorder = TreemapInteractionVisuals.GetContrastBorderColor(darkFill);

        Assert.True(GetBrightness(lightFillBorder) < GetBrightness(lightFill));
        Assert.True(GetBrightness(darkFillBorder) > GetBrightness(darkFill));
    }

    [Fact]
    public void GetAccentThickness_Selected_IsStrongerThanHover()
    {
        Assert.Equal(0d, TreemapInteractionVisuals.GetAccentThickness(TreemapInteractionState.None));
        Assert.Equal(1d, TreemapInteractionVisuals.GetAccentThickness(TreemapInteractionState.Hovered));
        Assert.Equal(2d, TreemapInteractionVisuals.GetAccentThickness(TreemapInteractionState.Selected));
    }

    [Fact]
    public void ShouldDrawStripeOverlay_OnlyForSelectedTilesWithEnoughSpace()
    {
        Assert.False(TreemapInteractionVisuals.ShouldDrawStripeOverlay(new Rect(0, 0, 80, 40), TreemapInteractionState.Hovered));
        Assert.False(TreemapInteractionVisuals.ShouldDrawStripeOverlay(new Rect(0, 0, 20, 40), TreemapInteractionState.Selected));
        Assert.False(TreemapInteractionVisuals.ShouldDrawStripeOverlay(new Rect(0, 0, 80, 18), TreemapInteractionState.Selected));
        Assert.True(TreemapInteractionVisuals.ShouldDrawStripeOverlay(new Rect(0, 0, 80, 40), TreemapInteractionState.Selected));
    }

    [Fact]
    public void GetStripeBounds_StaysInsideTileAfterAccentInset()
    {
        var bounds = new Rect(10, 20, 80, 40);

        var stripeBounds = TreemapInteractionVisuals.GetStripeBounds(bounds, accentThickness: 2);

        Assert.True(stripeBounds.X > bounds.X);
        Assert.True(stripeBounds.Y > bounds.Y);
        Assert.True(stripeBounds.Right < bounds.Right);
        Assert.True(stripeBounds.Bottom < bounds.Bottom);
    }

    [Fact]
    public void GetStripeColor_PicksLowAlphaContrastStroke()
    {
        var lightFill = Color.FromRgb(0xE2, 0xEA, 0xF2);
        var darkFill = Color.FromRgb(0x28, 0x34, 0x42);

        var lightStripe = TreemapInteractionVisuals.GetStripeColor(lightFill);
        var darkStripe = TreemapInteractionVisuals.GetStripeColor(darkFill);

        Assert.True(lightStripe.A < 0x80);
        Assert.True(darkStripe.A < 0x80);
        Assert.True(GetBrightness(lightStripe) < GetBrightness(lightFill));
        Assert.True(GetBrightness(darkStripe) > GetBrightness(darkFill));
    }

    private static double GetBrightness(Color color) =>
        (color.R * 299d) + (color.G * 587d) + (color.B * 114d);
}
