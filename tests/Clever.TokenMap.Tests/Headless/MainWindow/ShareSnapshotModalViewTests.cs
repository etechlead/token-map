using Avalonia;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.App.Views;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class ShareSnapshotModalViewTests
{
    [AvaloniaTheory]
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

    [Fact]
    public void CreateClipboardBitmap_ThrowsForNullSource()
    {
        Assert.Throws<ArgumentNullException>(() => ShareSnapshotModalView.CreateClipboardBitmap(null!));
    }

    [AvaloniaFact]
    public void RetainedClipboardResource_ReplacesAndDisposesPreviousValue()
    {
        var retention = new RetainedClipboardResource<TestDisposable>();
        var first = new TestDisposable();
        var second = new TestDisposable();

        retention.Replace(first);

        Assert.Same(first, retention.Current);
        Assert.False(first.IsDisposed);

        retention.Replace(second);

        Assert.True(first.IsDisposed);
        Assert.Same(second, retention.Current);
        Assert.False(second.IsDisposed);

        retention.Clear();

        Assert.True(second.IsDisposed);
        Assert.Null(retention.Current);
    }
    private sealed class TestDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
