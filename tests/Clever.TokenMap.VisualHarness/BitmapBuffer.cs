using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

internal sealed record BitmapBuffer(int Width, int Height, int RowBytes, byte[] Pixels)
{
    public static BitmapBuffer FromBitmap(Bitmap bitmap)
    {
        using var writable = new WriteableBitmap(
            bitmap.PixelSize,
            bitmap.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = writable.Lock();
        bitmap.CopyPixels(framebuffer, AlphaFormat.Premul);

        var pixels = new byte[framebuffer.RowBytes * framebuffer.Size.Height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
        return new BitmapBuffer(framebuffer.Size.Width, framebuffer.Size.Height, framebuffer.RowBytes, pixels);
    }

    public static BitmapBuffer Create(int width, int height, byte[] pixels) =>
        new(width, height, width * 4, pixels);

    public WriteableBitmap ToWriteableBitmap()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(Width, Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        Marshal.Copy(Pixels, 0, framebuffer.Address, Pixels.Length);
        return bitmap;
    }
}
