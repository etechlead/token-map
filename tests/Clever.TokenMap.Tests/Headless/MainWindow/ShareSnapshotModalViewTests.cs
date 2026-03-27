using Avalonia;
using Clever.TokenMap.App.Views;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class ShareSnapshotModalViewTests
{
    [Theory]
    [InlineData(600, 336, 1.0, 1200, 672, 192, 192)]
    [InlineData(600, 336, 1.25, 1200, 672, 192, 192)]
    [InlineData(600, 336, 1.5, 1200, 672, 192, 192)]
    [InlineData(600, 336, 2.5, 1500, 840, 240, 240)]
    public void GetBitmapExportSettings_UsesHiDpiScaleWithTwoXMinimum(
        double logicalWidth,
        double logicalHeight,
        double renderScaling,
        int expectedPixelWidth,
        int expectedPixelHeight,
        double expectedDpiX,
        double expectedDpiY)
    {
        var settings = ShareSnapshotModalView.GetBitmapExportSettings(
            new Size(logicalWidth, logicalHeight),
            renderScaling);

        Assert.Equal(new PixelSize(expectedPixelWidth, expectedPixelHeight), settings.PixelSize);
        Assert.Equal(new Vector(expectedDpiX, expectedDpiY), settings.Dpi);
    }
}
