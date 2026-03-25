using Avalonia.Controls;
using Avalonia.Input;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
