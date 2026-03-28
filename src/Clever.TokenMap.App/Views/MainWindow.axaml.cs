using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void SettingsBackdrop_OnPointerPressed(object? sender, PointerPressedEventArgs? e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CloseSettingsCommand.Execute(null);
            if (e is not null)
            {
                e.Handled = true;
            }
        }
    }

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Escape || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsShareSnapshotOpen)
        {
            viewModel.CloseShareSnapshotCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (viewModel.ExcludesEditor.IsOpen)
        {
            viewModel.CancelExcludesEditorCommand.Execute(null);
            e.Handled = true;
        }
    }
}
