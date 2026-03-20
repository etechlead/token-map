using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;

namespace Clever.TokenMap.HeadlessTests;

public sealed class MainWindowLayoutTests
{
    [AvaloniaFact]
    public void MainWindow_ContainsMvpShellSections()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        window.Show();

        Assert.NotNull(window.FindControl<Control>("ToolbarHost"));
        Assert.NotNull(window.FindControl<Control>("ProjectTreePane"));
        Assert.NotNull(window.FindControl<Control>("TreemapPane"));
        Assert.NotNull(window.FindControl<Control>("DetailsPane"));
        Assert.NotNull(window.FindControl<Control>("StatusStrip"));
    }
}
