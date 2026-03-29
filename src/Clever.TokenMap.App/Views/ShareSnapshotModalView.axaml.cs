using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Views;

public partial class ShareSnapshotModalView : UserControl
{
    private int _copyFeedbackVersion;
    private readonly RetainedClipboardResource<Bitmap> _retainedClipboardBitmap = new();

    public ShareSnapshotModalView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => _retainedClipboardBitmap.Clear();
    }

    private async void ShareSnapshotCopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ShareCardPreviewView>("ShareCardPreviewControl") is not { } preview ||
            preview.FindControl<Control>("ShareCardRoot") is not { } shareCardRoot ||
            sender is not Button copyButton)
        {
            SetCopyFeedbackState(ShareCopyFeedbackState.Error);
            return;
        }

        copyButton.IsEnabled = false;

        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                SetCopyFeedbackState(ShareCopyFeedbackState.Error);
                return;
            }

            shareCardRoot.InvalidateVisual();
            UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);

            var renderScaling = Math.Max(1d, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d);
            var exportSettings = GetBitmapExportSettings(shareCardRoot.Bounds.Size, renderScaling);
            using var renderTargetBitmap = new RenderTargetBitmap(exportSettings.PixelSize, exportSettings.Dpi);
            renderTargetBitmap.Render(shareCardRoot);

            var bitmap = CreateClipboardBitmap(renderTargetBitmap);
            try
            {
                await clipboard.SetBitmapAsync(bitmap);
                await clipboard.FlushAsync();

                // Keep the rendered bitmap alive after the copy call because non-Windows
                // clipboards may request the image lazily when the user pastes elsewhere.
                _retainedClipboardBitmap.Replace(bitmap);
                SetCopyFeedbackState(ShareCopyFeedbackState.Success);
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        catch
        {
            SetCopyFeedbackState(ShareCopyFeedbackState.Error);
        }
        finally
        {
            copyButton.IsEnabled = true;
        }
    }

    private void SetCopyFeedbackState(ShareCopyFeedbackState state)
    {
        if (DataContext is not MainWindowViewModel { ShareSnapshot: { } shareSnapshot })
        {
            return;
        }

        shareSnapshot.CopyFeedbackState = state;
        var version = ++_copyFeedbackVersion;
        _ = ResetCopyFeedbackAsync(shareSnapshot, version);
    }

    private async Task ResetCopyFeedbackAsync(ShareSnapshotViewModel shareSnapshot, int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(1.8));
        await Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (version == _copyFeedbackVersion)
                {
                    shareSnapshot.CopyFeedbackState = ShareCopyFeedbackState.Idle;
                }
            },
            DispatcherPriority.Background);
    }

    internal static BitmapExportSettings GetBitmapExportSettings(Size logicalSize, double renderScaling)
    {
        var exportScale = Math.Max(2d, renderScaling);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(logicalSize.Width * exportScale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(logicalSize.Height * exportScale));
        var dpi = new Vector(96d * exportScale, 96d * exportScale);
        return new BitmapExportSettings(new PixelSize(pixelWidth, pixelHeight), dpi);
    }

    internal static WriteableBitmap CreateClipboardBitmap(Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var bitmap = new WriteableBitmap(
            source.PixelSize,
            source.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        source.CopyPixels(framebuffer, AlphaFormat.Premul);
        return bitmap;
    }

    internal readonly record struct BitmapExportSettings(PixelSize PixelSize, Vector Dpi);
}

internal sealed class RetainedClipboardResource<T>
    where T : class, IDisposable
{
    private T? _current;

    internal T? Current => _current;

    internal void Replace(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var previous = _current;
        _current = value;
        previous?.Dispose();
    }

    internal void Clear()
    {
        var current = _current;
        _current = null;
        current?.Dispose();
    }
}
