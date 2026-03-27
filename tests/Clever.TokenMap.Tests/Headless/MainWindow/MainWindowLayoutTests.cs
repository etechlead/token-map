using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Treemap;
using PathShape = Avalonia.Controls.Shapes.Path;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

using Clever.TokenMap.Tests.Headless.Support;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class MainWindowLayoutTests
{
    [AvaloniaFact]
    public void MainWindow_Title_DefaultsToTokenMap_WhenNoFolderIsSelected()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        Assert.Equal("TokenMap", window.Title);
    }

    [AvaloniaFact]
    public async Task MainWindow_Title_UsesSelectedFolderName_AfterFolderIsOpened()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal("Demo - TokenMap", window.Title);
    }

    [AvaloniaFact]
    public void MainWindow_ContainsShellSections()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        Assert.NotNull(FindNamedDescendant<Control>(window, "ToolbarHost"));
        Assert.NotNull(FindNamedDescendant<Grid>(window, "WorkspaceHost"));
        Assert.NotNull(FindNamedDescendant<Control>(window, "ProjectTreePane"));
        Assert.NotNull(FindNamedDescendant<Control>(window, "TreemapPane"));
        Assert.NotNull(FindNamedDescendant<TreemapControl>(window, "ProjectTreemapControl"));
        Assert.NotNull(FindNamedDescendant<ProgressBar>(window, "StatusProgressBar"));
        Assert.NotNull(FindNamedDescendant<StackPanel>(window, "ToolbarActionsGroup"));
        Assert.NotNull(FindNamedDescendant<Button>(window, "ShareButton"));
        Assert.NotNull(FindNamedDescendant<Button>(window, "SettingsButton"));
        Assert.NotNull(FindNamedDescendant<SplitButton>(window, "OpenFolderSplitButton"));
    }

    [AvaloniaFact]
    public void MainWindow_TreemapHeaderMetricButtons_ReflectSelectedMetric()
    {
        var viewModel = CreateMainWindowViewModel();
        var window = new AppMainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var tokensButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricTokensButton");
        var linesButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricLinesButton");
        var sizeButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricSizeButton");

        Assert.NotNull(tokensButton);
        Assert.NotNull(linesButton);
        Assert.NotNull(sizeButton);
        Assert.True(tokensButton.IsChecked);
        Assert.False(linesButton.IsChecked);
        Assert.False(sizeButton.IsChecked);

        viewModel.Toolbar.IsSizeMetricSelected = true;
        window.UpdateLayout();
        Assert.False(tokensButton.IsChecked);
        Assert.False(linesButton.IsChecked);
        Assert.True(sizeButton.IsChecked);

        viewModel.Toolbar.IsLinesMetricSelected = true;
        window.UpdateLayout();
        Assert.False(tokensButton.IsChecked);
        Assert.True(linesButton.IsChecked);
        Assert.False(sizeButton.IsChecked);
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersEmptyState_WhenNoRecentFoldersAreLoaded()
    {
        var viewModel = CreateMainWindowViewModel();
        var window = new AppMainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var emptyState = FindNamedDescendant<Control>(window, "RecentFoldersEmptyState");
        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");

        Assert.NotNull(startSurface);
        Assert.NotNull(emptyState);
        Assert.NotNull(workspaceHost);
        Assert.True(viewModel.RecentFolders.ShowStartSurface);
        Assert.True(viewModel.RecentFolders.ShowEmptyState);
        Assert.True(startSurface.IsVisible);
        Assert.True(emptyState.IsVisible);
        Assert.True(workspaceHost.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_ShowsRecentFoldersStartSurface_WhenRecentFoldersAreLoaded()
    {
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            selectedFolderPath: "C:\\Demo",
            recentFolderPaths:
            [
                "C:\\RepoA",
                "C:\\RepoB",
            ]);
        var window = new AppMainWindow
        {
            DataContext = viewModel,
        };

        window.Show();

        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var emptyState = FindNamedDescendant<Control>(window, "RecentFoldersEmptyState");
        var workspaceHost = FindNamedDescendant<Grid>(window, "WorkspaceHost");
        var clearButton = FindNamedDescendant<Button>(window, "ClearRecentFoldersButton");

        Assert.NotNull(startSurface);
        Assert.NotNull(emptyState);
        Assert.NotNull(workspaceHost);
        Assert.NotNull(clearButton);
        Assert.True(viewModel.RecentFolders.ShowStartSurface);
        Assert.True(viewModel.RecentFolders.HasRecentFolders);
        Assert.False(viewModel.RecentFolders.ShowEmptyState);
        Assert.True(startSurface.IsVisible);
        Assert.False(emptyState.IsVisible);
        Assert.True(clearButton.IsVisible);
        Assert.True(workspaceHost.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_OpenFolderSplitButton_HasRecentFoldersFlyout()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var splitButton = FindNamedDescendant<SplitButton>(window, "OpenFolderSplitButton");

        Assert.NotNull(splitButton);
        Assert.NotNull(splitButton.Flyout);
    }

    [AvaloniaFact]
    public void MainWindow_OpenFolderSplitButton_UsesWhiteContentInLightTheme()
    {
        var application = Application.Current!;
        var previousThemeVariant = application.RequestedThemeVariant;
        application.RequestedThemeVariant = ThemeVariant.Light;

        try
        {
            var window = new AppMainWindow
            {
                DataContext = CreateMainWindowViewModel(),
            };

            window.Show();

            var splitButton = FindNamedDescendant<SplitButton>(window, "OpenFolderSplitButton");
            Assert.NotNull(splitButton);

            var openFolderText = splitButton.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => string.Equals(textBlock.Text, "Open Folder", StringComparison.Ordinal));
            var openFolderIcon = splitButton.GetVisualDescendants()
                .OfType<PathShape>()
                .Single(icon => icon.Classes.Contains("action-button-icon"));

            var textBrush = Assert.IsAssignableFrom<ISolidColorBrush>(openFolderText.Foreground);
            var iconBrush = Assert.IsAssignableFrom<ISolidColorBrush>(openFolderIcon.Fill);

            Assert.Equal(Colors.White, textBrush.Color);
            Assert.Equal(Colors.White, iconBrush.Color);
        }
        finally
        {
            application.RequestedThemeVariant = previousThemeVariant;
        }
    }

    [AvaloniaFact]
    public void MainWindow_InitialToolbarAvailability_DisablesMetricUntilSnapshotIsLoaded()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var metricTokensButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricTokensButton");
        var metricLinesButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricLinesButton");
        var metricSizeButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricSizeButton");
        var themeSystemButton = FindNamedDescendant<ToggleButton>(window, "ThemeSystemButton");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");

        Assert.NotNull(metricTokensButton);
        Assert.NotNull(metricLinesButton);
        Assert.NotNull(metricSizeButton);
        Assert.NotNull(themeSystemButton);
        Assert.NotNull(rescanButton);
        Assert.False(metricTokensButton.IsEnabled);
        Assert.False(metricLinesButton.IsEnabled);
        Assert.False(metricSizeButton.IsEnabled);
        Assert.True(metricTokensButton.IsChecked);
        Assert.True(themeSystemButton.IsEnabled);
        Assert.False(rescanButton.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_IsHiddenByDefaultAndTogglesFromViewModel()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var drawer = FindNamedDescendant<Control>(window, "SettingsDrawer");
        var backdrop = FindNamedDescendant<Control>(window, "SettingsBackdrop");
        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);

        Assert.NotNull(drawer);
        Assert.NotNull(backdrop);
        Assert.False(drawer.IsVisible);
        Assert.False(backdrop.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.True(drawer.IsVisible);
        Assert.True(backdrop.IsVisible);

        viewModel.ToggleSettingsCommand.Execute(null);
        Assert.False(drawer.IsVisible);
        Assert.False(backdrop.IsVisible);
    }

    [AvaloniaFact]
    public void MainWindow_GlobalExcludesEditor_OpensAndCancelsWithoutSaving()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        var modal = FindNamedDescendant<Control>(window, "ExcludesEditorModal");
        var backdrop = FindNamedDescendant<Control>(window, "ExcludesEditorBackdrop");
        var editor = FindNamedDescendant<TextBox>(window, "ExcludesEditorTextBox");

        Assert.NotNull(modal);
        Assert.NotNull(backdrop);
        Assert.NotNull(editor);
        Assert.True(modal.IsVisible);
        Assert.True(backdrop.IsVisible);
        Assert.Equal(
            string.Join(Environment.NewLine, GlobalExcludeDefaults.DefaultEntries).ReplaceLineEndings("\n"),
            editor.Text?.ReplaceLineEndings("\n"));

        viewModel.CancelExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        Assert.False(modal.IsVisible);
        Assert.False(backdrop.IsVisible);
        Assert.Equal(GlobalExcludeDefaults.DefaultEntries, viewModel.Toolbar.BuildScanOptions().GlobalExcludes);
    }

    [AvaloniaFact]
    public async Task MainWindow_GlobalExcludesSave_ShowsAndClearsRescanNotice()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = " node_modules\\\\ \n\n/src//generated/**\n!nested/scripts/";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        var notice = FindNamedDescendant<Control>(window, "ScanSettingsRescanNotice");
        var rescanButton = FindNamedDescendant<Button>(window, "ScanSettingsRescanButton");

        Assert.NotNull(notice);
        Assert.NotNull(rescanButton);
        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.True(notice.IsVisible);
        Assert.Collection(
            viewModel.Toolbar.BuildScanOptions().GlobalExcludes,
            entry => Assert.Equal("node_modules/", entry),
            entry => Assert.Equal("/src/generated/**", entry),
            entry => Assert.Equal("!nested/scripts/", entry));

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text += "\nobj/";
        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
        viewModel.CancelExcludesEditorCommand.Execute(null);

        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "vendor/";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);

        await viewModel.Toolbar.RescanCommand.ExecuteAsync(null);

        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.False(notice.IsVisible);
    }

    [AvaloniaFact]
    public async Task MainWindow_FolderExcludesSave_UsesSharedEditorAndShowsRescanNotice()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenFolderExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "/generated/\n!generated/keep.txt";
        viewModel.SaveExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        Assert.True(viewModel.ExcludesEditor.ShowRescanNotice);
        Assert.True(viewModel.Toolbar.BuildScanOptions().UseFolderExcludes);
        Assert.Collection(
            viewModel.Toolbar.BuildScanOptions().FolderExcludes,
            entry => Assert.Equal("/generated/", entry),
            entry => Assert.Equal("!generated/keep.txt", entry));

        await viewModel.Toolbar.RescanCommand.ExecuteAsync(null);

        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
    }

    [AvaloniaFact]
    public async Task MainWindow_SaveAndRescanExcludesEditor_RescansImmediately()
    {
        var analyzer = new CountingProjectAnalyzer(
            CreateSnapshot(),
            CreateSnapshot());
        var viewModel = CreateMainWindowViewModel(analyzer);
        var window = new AppMainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        viewModel.OpenFolderExcludesEditorCommand.Execute(null);
        viewModel.ExcludesEditor.Text = "/generated/";
        await viewModel.SaveAndRescanExcludesEditorCommand.ExecuteAsync(null);

        Assert.Equal(2, analyzer.CallCount);
        Assert.False(viewModel.ExcludesEditor.IsOpen);
        Assert.False(viewModel.ExcludesEditor.ShowRescanNotice);
    }

    [AvaloniaFact]
    public async Task MainWindow_OpenFolderFlow_PopulatesTreeAndSummary()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var statusStrip = FindNamedDescendant<Control>(window, "StatusStrip");
        var tokenSummaryText = FindNamedDescendant<TextBlock>(window, "TokenSummaryValueText");
        var lineSummaryText = FindNamedDescendant<TextBlock>(window, "LineSummaryValueText");
        var fileSummaryText = FindNamedDescendant<TextBlock>(window, "FileSummaryValueText");
        var metricTokensButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricTokensButton");
        var metricLinesButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricLinesButton");
        var metricSizeButton = FindNamedDescendant<ToggleButton>(window, "TreemapMetricSizeButton");
        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");

        Assert.NotNull(statusStrip);
        Assert.NotNull(tokenSummaryText);
        Assert.NotNull(lineSummaryText);
        Assert.NotNull(fileSummaryText);
        Assert.NotNull(metricTokensButton);
        Assert.NotNull(metricLinesButton);
        Assert.NotNull(metricSizeButton);
        Assert.NotNull(startSurface);
        Assert.NotNull(rescanButton);
        Assert.Equal(AnalysisState.Completed, viewModel.AnalysisState);
        Assert.Equal("42", tokenSummaryText.Text);
        Assert.Equal("11", lineSummaryText.Text);
        Assert.Equal("1", fileSummaryText.Text);
        Assert.Equal(2, viewModel.Tree.VisibleNodes.Count);
        Assert.True(metricTokensButton.IsEnabled);
        Assert.True(metricLinesButton.IsEnabled);
        Assert.True(metricSizeButton.IsEnabled);
        Assert.False(statusStrip.IsVisible);
        Assert.False(startSurface.IsVisible);
        Assert.True(rescanButton.IsVisible);
    }

    [AvaloniaFact]
    public async Task MainWindow_CancelCommand_ShowsProgressOnlyWhileScanIsRunning()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new CancelAwareProjectAnalyzer());
        window.DataContext = viewModel;

        window.Show();
        var statusStrip = FindNamedDescendant<Control>(window, "StatusStrip");
        var stopButton = FindNamedDescendant<Button>(window, "StopButton");

        Assert.NotNull(statusStrip);
        Assert.NotNull(stopButton);
        Assert.False(statusStrip.IsVisible);
        Assert.False(stopButton.IsVisible);

        var openTask = viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await Task.Delay(100);
        Assert.True(statusStrip.IsVisible);
        Assert.True(stopButton.IsVisible);
        viewModel.Toolbar.CancelCommand.Execute(null);
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, viewModel.AnalysisState);
        Assert.False(statusStrip.IsVisible);
        Assert.False(stopButton.IsVisible);
    }
}
