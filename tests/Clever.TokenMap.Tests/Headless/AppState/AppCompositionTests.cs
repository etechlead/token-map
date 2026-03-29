using Avalonia;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.App;
using Microsoft.Extensions.DependencyInjection;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class AppCompositionTests
{
    [AvaloniaFact]
    public void CreateServiceProvider_CanResolveMainWindow()
    {
        var application = Assert.IsType<App.App>(Application.Current);

        var serviceProvider = AppComposition.CreateServiceProvider(application);
        try
        {
            var exception = Record.Exception(() => serviceProvider.GetRequiredService<AppMainWindow>());

            Assert.Null(exception);
        }
        finally
        {
            (serviceProvider as IDisposable)?.Dispose();
        }
    }
}
