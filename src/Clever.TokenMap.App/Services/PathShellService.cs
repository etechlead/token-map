using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Clever.TokenMap.App.Services;

public static class PathShellService
{
    public static IPathShellService CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPathShellService();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsPathShellService();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxPathShellService();
        }

        return new UnsupportedPathShellService();
    }

    private sealed class WindowsPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal in Explorer";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return Task.FromResult(false);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true,
                });

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
                var arguments = $"/select,\"{fullPath}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                });

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }

    private sealed class MacOsPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal in Finder";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return Task.FromResult(false);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { fullPath },
                });

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
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { "-R", fullPath },
                });

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }

    private sealed class LinuxPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal in File Manager";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return Task.FromResult(false);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { fullPath },
                });

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
                var targetPath = isDirectory
                    ? fullPath
                    : Path.GetDirectoryName(fullPath) ?? fullPath;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { targetPath },
                });

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }

    private sealed class UnsupportedPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
