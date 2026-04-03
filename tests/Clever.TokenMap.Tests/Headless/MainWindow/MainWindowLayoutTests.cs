using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Treemap;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class MainWindowLayoutTests
{
    [AvaloniaFact]
    public void MainWindow_Title_IsPresent_WhenNoFolderIsSelected()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        Assert.False(string.IsNullOrWhiteSpace(window.Title));
    }

    [AvaloniaFact]
    public async Task MainWindow_Title_IncludesSelectedFolderName_AfterFolderIsOpened()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(CreateSnapshot()));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        Assert.Contains("Demo", window.Title, StringComparison.Ordinal);
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
        Assert.NotNull(FindNamedDescendant<Border>(window, "ProgressStatusPill"));
        Assert.NotNull(FindNamedDescendant<TextBlock>(window, "ProgressStatusPillText"));
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

        var tokensButton = FindTreemapMetricButton(window, MetricIds.Tokens);
        var linesButton = FindTreemapMetricButton(window, MetricIds.NonEmptyLines);
        var sizeButton = FindTreemapMetricButton(window, MetricIds.FileSizeBytes);
        var showValuesCheckBox = FindNamedDescendant<CheckBox>(window, "TreemapShowValuesCheckBox");

        Assert.NotNull(tokensButton);
        Assert.NotNull(linesButton);
        Assert.NotNull(sizeButton);
        Assert.NotNull(showValuesCheckBox);
        Assert.True(tokensButton.IsChecked);
        Assert.False(linesButton.IsChecked);
        Assert.False(sizeButton.IsChecked);
        Assert.True(showValuesCheckBox.IsChecked);

        viewModel.Toolbar.SelectedMetric = MetricIds.FileSizeBytes;
        window.UpdateLayout();
        Assert.False(tokensButton.IsChecked);
        Assert.False(linesButton.IsChecked);
        Assert.True(sizeButton.IsChecked);

        viewModel.Toolbar.SelectedMetric = MetricIds.NonEmptyLines;
        window.UpdateLayout();
        Assert.False(tokensButton.IsChecked);
        Assert.True(linesButton.IsChecked);
        Assert.False(sizeButton.IsChecked);

        viewModel.Toolbar.ShowTreemapMetricValues = false;
        window.UpdateLayout();
        Assert.False(showValuesCheckBox.IsChecked);
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
    public void MainWindow_OpenFolderSplitButton_UsesConsistentContentBrushesInLightTheme()
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
                .Single(textBlock => textBlock.Classes.Contains("action-button-label"));
            var openFolderIcon = splitButton.GetVisualDescendants()
                .OfType<PathIcon>()
                .Single(icon => icon.Classes.Contains("action-button-icon"));

            var textBrush = Assert.IsAssignableFrom<ISolidColorBrush>(openFolderText.Foreground);
            var iconBrush = Assert.IsAssignableFrom<ISolidColorBrush>(openFolderIcon.Foreground);

            Assert.True(textBrush.Color.A > 0);
            Assert.Equal(textBrush.Color, iconBrush.Color);
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

        var metricTokensButton = FindTreemapMetricButton(window, MetricIds.Tokens);
        var metricLinesButton = FindTreemapMetricButton(window, MetricIds.NonEmptyLines);
        var metricSizeButton = FindTreemapMetricButton(window, MetricIds.FileSizeBytes);
        var showValuesCheckBox = FindNamedDescendant<CheckBox>(window, "TreemapShowValuesCheckBox");
        var themeSystemButton = FindNamedDescendant<ToggleButton>(window, "ThemeSystemButton");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");

        Assert.NotNull(metricTokensButton);
        Assert.NotNull(metricLinesButton);
        Assert.NotNull(metricSizeButton);
        Assert.NotNull(showValuesCheckBox);
        Assert.NotNull(themeSystemButton);
        Assert.NotNull(rescanButton);
        Assert.False(metricTokensButton.IsEnabled);
        Assert.False(metricLinesButton.IsEnabled);
        Assert.False(metricSizeButton.IsEnabled);
        Assert.False(showValuesCheckBox.IsEnabled);
        Assert.True(metricTokensButton.IsChecked);
        Assert.True(showValuesCheckBox.IsChecked);
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
    public void MainWindow_SettingsDrawer_ShowsAboutCard()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.ToggleSettingsCommand.Execute(null);
        window.UpdateLayout();

        var aboutCard = FindNamedDescendant<Border>(window, "AboutCard");
        var productName = FindNamedDescendant<TextBlock>(window, "AboutProductNameText");
        var version = FindNamedDescendant<TextBlock>(window, "AboutVersionText");
        var description = FindNamedDescendant<TextBlock>(window, "AboutDescriptionText");
        var repositoryButton = FindNamedDescendant<Button>(window, "AboutRepositoryButton");
        var license = FindNamedDescendant<TextBlock>(window, "AboutLicenseText");

        Assert.NotNull(aboutCard);
        Assert.NotNull(productName);
        Assert.NotNull(version);
        Assert.NotNull(description);
        Assert.NotNull(repositoryButton);
        Assert.NotNull(license);
        Assert.True(aboutCard.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(productName.Text));
        Assert.False(string.IsNullOrWhiteSpace(version.Text));
        Assert.StartsWith("v", version.Text);
        Assert.False(string.IsNullOrWhiteSpace(description.Text));
        Assert.False(string.IsNullOrWhiteSpace(license.Text));
    }

    [AvaloniaFact]
    public void MainWindow_SettingsDrawer_ShowsTreeOnlyMetricsLegend()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.ToggleSettingsCommand.Execute(null);
        window.UpdateLayout();

        var legend = FindNamedDescendant<TextBlock>(window, "TreeOnlyMetricsLegendText");
        var divider = FindNamedDescendant<Grid>(window, "TreeOnlyMetricsLegendPanel");
        var treeOnlyItems = FindNamedDescendant<ItemsControl>(window, "TreeOnlyMetricsItemsControl");

        Assert.NotNull(legend);
        Assert.NotNull(divider);
        Assert.NotNull(treeOnlyItems);
        Assert.True(legend.IsVisible);
        Assert.True(divider.IsVisible);
        Assert.True(treeOnlyItems.IsVisible);
        Assert.Equal("Tree only", legend.Text);
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
    public void MainWindow_Escape_ClosesExcludesEditorModal()
    {
        var window = new AppMainWindow
        {
            DataContext = CreateMainWindowViewModel(),
        };

        window.Show();

        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        viewModel.OpenGlobalExcludesEditorCommand.Execute(null);
        window.UpdateLayout();

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = window,
            Key = Key.Escape,
        };

        window.RaiseEvent(keyArgs);
        window.UpdateLayout();

        Assert.True(keyArgs.Handled);
        Assert.False(viewModel.ExcludesEditor.IsOpen);
    }

    [AvaloniaFact]
    public async Task MainWindow_Escape_ClosesShareSnapshotModal()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: "C:\\Demo");
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        viewModel.OpenShareSnapshotCommand.Execute(null);
        window.UpdateLayout();

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = window,
            Key = Key.Escape,
        };

        window.RaiseEvent(keyArgs);
        window.UpdateLayout();

        Assert.True(keyArgs.Handled);
        Assert.False(viewModel.IsShareSnapshotOpen);
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
        var progressPill = FindNamedDescendant<Control>(window, "ProgressStatusPill");
        var tokenSummaryText = FindNamedDescendant<TextBlock>(window, "TokenSummaryValueText");
        var lineSummaryText = FindNamedDescendant<TextBlock>(window, "LineSummaryValueText");
        var fileSummaryText = FindNamedDescendant<TextBlock>(window, "FileSummaryValueText");
        var metricTokensButton = FindTreemapMetricButton(window, MetricIds.Tokens);
        var metricLinesButton = FindTreemapMetricButton(window, MetricIds.NonEmptyLines);
        var metricSizeButton = FindTreemapMetricButton(window, MetricIds.FileSizeBytes);
        var startSurface = FindNamedDescendant<Control>(window, "RecentFoldersStartSurface");
        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");

        Assert.NotNull(statusStrip);
        Assert.NotNull(progressPill);
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
        Assert.False(progressPill.IsVisible);
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
        var progressPill = FindNamedDescendant<Control>(window, "ProgressStatusPill");
        var progressPillText = FindNamedDescendant<TextBlock>(window, "ProgressStatusPillText");
        var stopButton = FindNamedDescendant<Button>(window, "StopButton");

        Assert.NotNull(statusStrip);
        Assert.NotNull(progressPill);
        Assert.NotNull(progressPillText);
        Assert.NotNull(stopButton);
        Assert.False(statusStrip.IsVisible);
        Assert.False(progressPill.IsVisible);
        Assert.False(stopButton.IsVisible);

        var openTask = viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await Task.Delay(100);
        Assert.True(statusStrip.IsVisible);
        Assert.True(progressPill.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(progressPillText.Text));
        Assert.True(stopButton.IsVisible);
        viewModel.Toolbar.CancelCommand.Execute(null);
        await openTask;

        Assert.Equal(AnalysisState.Cancelled, viewModel.AnalysisState);
        Assert.False(statusStrip.IsVisible);
        Assert.False(progressPill.IsVisible);
        Assert.False(stopButton.IsVisible);
    }

    [AvaloniaFact]
    public async Task MainWindow_RescanButton_HidesWhileRescanIsRunning()
    {
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(new RescanAwareProjectAnalyzer());
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var rescanButton = FindNamedDescendant<Button>(window, "RescanButton");
        var stopButton = FindNamedDescendant<Button>(window, "StopButton");

        Assert.NotNull(rescanButton);
        Assert.NotNull(stopButton);
        Assert.True(rescanButton.IsVisible);
        Assert.False(stopButton.IsVisible);

        var rescanTask = viewModel.Toolbar.RescanCommand.ExecuteAsync(null);
        await Task.Delay(100);

        Assert.False(rescanButton.IsVisible);
        Assert.True(stopButton.IsVisible);

        viewModel.Toolbar.CancelCommand.Execute(null);
        await rescanTask;

        Assert.Equal(AnalysisState.Cancelled, viewModel.AnalysisState);
        Assert.True(rescanButton.IsVisible);
        Assert.False(stopButton.IsVisible);
    }

    private sealed class RescanAwareProjectAnalyzer : IProjectAnalyzer
    {
        private int _callCount;

        public async Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken)
        {
            var callCount = Interlocked.Increment(ref _callCount);
            if (callCount == 1)
            {
                return CreateSnapshot();
            }

            progress?.Report(new AnalysisProgress(
                "ScanningTree",
                1,
                null,
                "Program.cs",
                DiscoveredFileCount: 1));
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            throw new InvalidOperationException("This path should have been cancelled.");
        }
    }

    private static ToggleButton? FindTreemapMetricButton(Window window, MetricId metricId)
    {
        var expectedLabel = DefaultMetricCatalog.Instance
            .GetAll()
            .FirstOrDefault(definition => definition.Id == metricId)?
            .ShortName;
        if (string.IsNullOrWhiteSpace(expectedLabel))
        {
            return null;
        }

        return window.GetVisualDescendants()
            .OfType<ToggleButton>()
            .FirstOrDefault(button =>
                button.Classes.Contains("treemap-metric-button") &&
                string.Equals(button.Content?.ToString(), expectedLabel, StringComparison.Ordinal));
    }
}
