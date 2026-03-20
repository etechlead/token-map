using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Clever.TokenMap.App.Services;

public sealed class WindowFolderPickerService(Window window) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
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
