using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

    [Fact]
    public void CreateClipboardBitmap_CopiesPixelsIntoDetachedBitmap()
    {
        using var source = new WriteableBitmap(
            new PixelSize(2, 1),
            new Vector(144, 144),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        var originalPixels =
            new byte[]
            {
                0x10, 0x20, 0x30, 0x40,
                0x50, 0x60, 0x70, 0x80,
            };
        var updatedPixels =
            new byte[]
            {
                0x80, 0x70, 0x60, 0x50,
                0x40, 0x30, 0x20, 0x10,
            };

        WritePixels(source, originalPixels);

        using var clipboardBitmap = ShareSnapshotModalView.CreateClipboardBitmap(source);

        WritePixels(source, updatedPixels);

        Assert.Equal(source.PixelSize, clipboardBitmap.PixelSize);
        Assert.Equal(source.Dpi, clipboardBitmap.Dpi);
        Assert.Equal(originalPixels, ReadPixels(clipboardBitmap, originalPixels.Length));
    }

    [Fact]
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

    private static void WritePixels(WriteableBitmap bitmap, byte[] pixels)
    {
        using var framebuffer = bitmap.Lock();
        Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap, int pixelCount)
    {
        using var framebuffer = bitmap.Lock();
        var pixels = new byte[pixelCount];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
        return pixels;
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
