using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Clever.TokenMap.App.Services;

public sealed class DesktopApplicationControlService : IApplicationControlService
{
    public void RequestShutdown(int exitCode = 0)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
            return;
        }

        Environment.Exit(exitCode);
    }
}
