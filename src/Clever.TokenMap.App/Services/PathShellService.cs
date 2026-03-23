using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Clever.TokenMap.App.Services;

public sealed class PathShellService : IPathShellService
{
    public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { fullPath },
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { fullPath },
                });
            }
            else
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var arguments = $"/select,\"{fullPath}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { "-R", fullPath },
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                var targetPath = isDirectory
                    ? fullPath
                    : Path.GetDirectoryName(fullPath) ?? fullPath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { targetPath },
                });
            }
            else
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
