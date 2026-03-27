using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Clever.TokenMap.App.ViewModels;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Treemap;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class ShareSnapshotModalTests
{
    [AvaloniaFact]
    public async Task ShareSnapshotModal_ProjectNameControlsTrackCheckboxState()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: "C:\\Demo");
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.OpenShareSnapshotCommand.Execute(null);
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "ShareSnapshotModal");
        var backdrop = FindNamedDescendant<Control>(window, "ShareSnapshotBackdrop");
        var closeModalButton = FindNamedDescendant<Button>(window, "CloseShareSnapshotModalButton");
        var closeActionButton = FindNamedDescendant<Button>(window, "CloseShareSnapshotButton");
        var projectNameTextBox = FindNamedDescendant<TextBox>(window, "ProjectNameTextBox");
        var projectTitleText = FindNamedDescendant<TextBlock>(window, "ShareProjectTitleText");
        var includeProjectNameCheckBox = FindNamedDescendant<CheckBox>(window, "IncludeProjectNameCheckBox");

        Assert.NotNull(modal);
        Assert.NotNull(backdrop);
        Assert.NotNull(closeModalButton);
        Assert.NotNull(projectNameTextBox);
        Assert.NotNull(projectTitleText);
        Assert.NotNull(includeProjectNameCheckBox);
        Assert.True(modal.IsVisible);
        Assert.True(backdrop.IsVisible);
        Assert.Null(closeActionButton);
        Assert.True(includeProjectNameCheckBox.IsChecked ?? false);
        Assert.True(projectNameTextBox.IsVisible);
        Assert.True(viewModel.ShareSnapshot?.ShowProjectName ?? false);
        Assert.False(viewModel.ShareSnapshot?.ShowProjectNamePlaceholder ?? true);
        Assert.True(projectTitleText.IsVisible);
        Assert.Equal("Demo", projectTitleText.Text);

        viewModel.ShareSnapshot!.IncludeProjectName = false;
        window.UpdateLayout();

        Assert.False(projectNameTextBox.IsVisible);
        Assert.False(viewModel.ShareSnapshot.ShowProjectName);
        Assert.True(viewModel.ShareSnapshot.ShowProjectNamePlaceholder);
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_ProjectNamePreviewUpdatesWhenUserEditsTitle()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: "C:\\Demo");
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.OpenShareSnapshotCommand.Execute(null);
        viewModel.ShareSnapshot!.IncludeProjectName = true;
        viewModel.ShareSnapshot.ProjectName = "TokenMap Desktop";
        window.UpdateLayout();

        var projectTitleText = FindNamedDescendant<TextBlock>(window, "ShareProjectTitleText");

        Assert.NotNull(projectTitleText);
        Assert.True(projectTitleText.IsVisible);
        Assert.Equal("TokenMap Desktop", projectTitleText.Text);
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_UsesSingleRoundedLayoutWithoutPresetControls()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: "C:\\Demo");
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.OpenShareSnapshotCommand.Execute(null);
        window.UpdateLayout();

        var roundedLayout = FindNamedDescendant<Control>(window, "RoundedCardLayout");
        var cleanPresetRadio = FindNamedDescendant<RadioButton>(window, "SharePresetCleanRadioButton");
        var neonPresetRadio = FindNamedDescendant<RadioButton>(window, "SharePresetNeonRadioButton");
        var roundedPresetRadio = FindNamedDescendant<RadioButton>(window, "SharePresetRoundedRadioButton");

        Assert.NotNull(roundedLayout);
        Assert.True(roundedLayout.IsVisible);
        Assert.Null(cleanPresetRadio);
        Assert.Null(neonPresetRadio);
        Assert.Null(roundedPresetRadio);
        Assert.Equal(TreemapPalette.Plain, viewModel.ShareSnapshot?.PreviewTreemapPalette);
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_UsesLightCardPalette_WhenApplicationThemeIsLight()
    {
        var application = Application.Current!;
        var previousThemeVariant = application.RequestedThemeVariant;
        application.RequestedThemeVariant = ThemeVariant.Light;

        try
        {
            var window = new AppMainWindow();
            var viewModel = CreateMainWindowViewModel(selectedFolderPath: "C:\\Demo");
            window.DataContext = viewModel;

            window.Show();
            await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
            viewModel.OpenShareSnapshotCommand.Execute(null);
            window.UpdateLayout();

            var shareCardRoot = FindNamedDescendant<Border>(window, "ShareCardRoot");
            var projectTitleText = FindNamedDescendant<TextBlock>(window, "ShareProjectTitleText");
            var shareTreemap = FindNamedDescendant<TreemapControl>(window, "RoundedShareTreemapControl");

            Assert.NotNull(shareCardRoot);
            Assert.NotNull(projectTitleText);
            Assert.NotNull(shareTreemap);

            var backgroundBrush = Assert.IsType<LinearGradientBrush>(shareCardRoot.Background);
            var titleBrush = Assert.IsAssignableFrom<ISolidColorBrush>(projectTitleText.Foreground);
            var treemapHost = Assert.IsType<Border>(shareTreemap.Parent);
            var treemapHostBrush = Assert.IsAssignableFrom<ISolidColorBrush>(treemapHost.Background);
            var treemapCanvasBrush = Assert.IsAssignableFrom<ISolidColorBrush>(shareTreemap.CanvasBackgroundBrush);

            Assert.Equal(Color.Parse("#F7FAFD"), backgroundBrush.GradientStops[0].Color);
            Assert.Equal(Color.Parse("#E9EFF6"), backgroundBrush.GradientStops[1].Color);
            Assert.Equal(Color.Parse("#18212B"), titleBrush.Color);
            Assert.Equal(Colors.Transparent, treemapHostBrush.Color);
            Assert.Equal(Colors.Transparent, treemapCanvasBrush.Color);
        }
        finally
        {
            application.RequestedThemeVariant = previousThemeVariant;
        }
    }
}
