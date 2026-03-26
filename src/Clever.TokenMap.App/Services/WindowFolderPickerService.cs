using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Clever.TokenMap.App.Services;

public sealed class WindowFolderPickerService : IFolderPickerService
{
    private readonly Func<Window?> _windowAccessor;

    public WindowFolderPickerService(Func<Window?> windowAccessor)
    {
        ArgumentNullException.ThrowIfNull(windowAccessor);
        _windowAccessor = windowAccessor;
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (_windowAccessor() is not { } window)
        {
            return null;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select project folder",
        });

        cancellationToken.ThrowIfCancellationRequested();

        return folders.Count > 0
            ? folders[0].TryGetLocalPath()
            : null;
    }
}
