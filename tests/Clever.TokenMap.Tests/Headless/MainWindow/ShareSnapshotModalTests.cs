using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Tests.Headless.Support;
using Clever.TokenMap.Tests.Support;
using Clever.TokenMap.Treemap;
using AppMainWindow = Clever.TokenMap.App.Views.MainWindow;
using PathShape = Avalonia.Controls.Shapes.Path;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class ShareSnapshotModalTests
{
    [AvaloniaFact]
    public async Task ShareSnapshotModal_ProjectNameControlsTrackCheckboxState()
    {
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
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
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
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
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
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
    public async Task ShareSnapshotModal_ResolvesLightThemeBrushes_WhenApplicationThemeIsLight()
    {
        var application = Application.Current!;
        var previousThemeVariant = application.RequestedThemeVariant;
        application.RequestedThemeVariant = ThemeVariant.Light;

        try
        {
            var window = new AppMainWindow();
            var viewModel = CreateMainWindowViewModel(selectedFolderPath: GetTestFolderPath("Demo"));
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

            Assert.True(backgroundBrush.GradientStops.Count >= 2);
            Assert.All(backgroundBrush.GradientStops, stop => Assert.True(stop.Color.A > 0));
            Assert.True(titleBrush.Color.A > 0);
            Assert.NotNull(treemapHost.Background);
            Assert.NotNull(shareTreemap.CanvasBackgroundBrush);
        }
        finally
        {
            application.RequestedThemeVariant = previousThemeVariant;
        }
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_ProjectTitle_KeepsVisibleGapAboveTokenValue()
    {
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
        window.DataContext = viewModel;

        try
        {
            window.Show();
            await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
            viewModel.OpenShareSnapshotCommand.Execute(null);
            window.UpdateLayout();

            var titleBounds = GetRenderedBounds<TextBlock, Border>(window, "ShareProjectTitleText", "ShareCardRoot");
            var tokenBounds = GetRenderedBounds<TextBlock, Border>(window, "ShareTokenValueText", "ShareCardRoot");
            var gap = tokenBounds.Top - titleBounds.Bottom;

            Assert.True(
                gap >= 8,
                $"Expected share card title and token value to keep a visible vertical gap, got {gap:F2}px.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_ProjectNameTextBox_UsesCenteredSingleLineLayout()
    {
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
        window.DataContext = viewModel;

        try
        {
            window.Show();
            await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
            viewModel.OpenShareSnapshotCommand.Execute(null);
            window.UpdateLayout();

            var projectNameTextBox = FindNamedDescendant<TextBox>(window, "ProjectNameTextBox");

            Assert.NotNull(projectNameTextBox);
            Assert.Equal(VerticalAlignment.Center, projectNameTextBox.VerticalContentAlignment);
            Assert.Equal(new Thickness(10, 0), projectNameTextBox.Padding);

            var textPresenter = projectNameTextBox
                .GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => control.GetType().Name == "TextPresenter");

            Assert.NotNull(textPresenter);

            var presenterTransform = textPresenter.TransformToVisual(projectNameTextBox);
            Assert.NotNull(presenterTransform);

            var presenterBounds = new Rect(textPresenter.Bounds.Size).TransformToAABB(presenterTransform.Value);
            var textBoxCenterY = projectNameTextBox.Bounds.Height / 2d;

            Assert.True(
                Math.Abs(presenterBounds.Center.Y - textBoxCenterY) <= 2,
                $"Expected share project name text presenter to stay vertically centered, got presenterCenterY={presenterBounds.Center.Y:F2}, textBoxCenterY={textBoxCenterY:F2}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_FooterElements_StayWithinFooterRow()
    {
        var demoRootPath = GetTestFolderPath("Demo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(selectedFolderPath: demoRootPath);
        window.DataContext = viewModel;

        try
        {
            window.Show();
            await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
            viewModel.OpenShareSnapshotCommand.Execute(null);
            window.UpdateLayout();

            var shareCardRoot = FindNamedDescendant<Border>(window, "ShareCardRoot");
            var footerRow = FindNamedDescendant<Grid>(window, "ShareFooterRow");
            var footerLeadText = FindNamedDescendant<TextBlock>(window, "ShareFooterLeadText");
            var githubIcon = FindNamedDescendant<PathShape>(window, "ShareFooterGithubIcon");
            var repositoryText = FindNamedDescendant<TextBlock>(window, "ShareFooterRepositoryText");

            Assert.NotNull(shareCardRoot);
            Assert.NotNull(footerRow);
            Assert.NotNull(footerLeadText);
            Assert.NotNull(githubIcon);
            Assert.NotNull(repositoryText);

            var rowTransform = footerRow.TransformToVisual(shareCardRoot);
            var leadTransform = footerLeadText.TransformToVisual(shareCardRoot);
            var iconTransform = githubIcon.TransformToVisual(shareCardRoot);
            var textTransform = repositoryText.TransformToVisual(shareCardRoot);
            Assert.NotNull(rowTransform);
            Assert.NotNull(leadTransform);
            Assert.NotNull(iconTransform);
            Assert.NotNull(textTransform);

            var rowBounds = new Rect(footerRow.Bounds.Size).TransformToAABB(rowTransform.Value);
            var leadBounds = new Rect(footerLeadText.Bounds.Size).TransformToAABB(leadTransform.Value);
            var iconBounds = new Rect(githubIcon.Bounds.Size).TransformToAABB(iconTransform.Value);
            var textBounds = new Rect(repositoryText.Bounds.Size).TransformToAABB(textTransform.Value);

            Assert.True(
                leadBounds.Top >= rowBounds.Top - 0.5 && leadBounds.Bottom <= rowBounds.Bottom + 0.5,
                $"Expected share footer lead text to remain within the footer row, got lead={leadBounds}, row={rowBounds}.");
            Assert.True(
                iconBounds.Top >= rowBounds.Top - 0.5 && iconBounds.Bottom <= rowBounds.Bottom + 0.5,
                $"Expected share footer GitHub icon to remain within the footer row, got icon={iconBounds}, row={rowBounds}.");
            Assert.True(
                textBounds.Top >= rowBounds.Top - 0.5 && textBounds.Bottom <= rowBounds.Bottom + 0.5,
                $"Expected share footer repository text to remain within the footer row, got text={textBounds}, row={rowBounds}.");
            Assert.True(
                leadBounds.Right <= iconBounds.X + 0.5,
                $"Expected share footer lead text to stay before the icon, got lead={leadBounds}, icon={iconBounds}.");
            Assert.True(
                iconBounds.Right <= textBounds.X + 0.5,
                $"Expected share footer icon to stay before the repository text, got icon={iconBounds}, text={textBounds}.");
            Assert.True(
                Math.Abs(iconBounds.Center.Y - textBounds.Center.Y) <= 1.5,
                $"Expected share footer icon and repository text to stay vertically centered together, got icon={iconBounds}, text={textBounds}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_MetricValues_StayWithinHosts_ForRepresentativeLengths()
    {
        var stressRepoPath = GetTestFolderPath("StressRepo");
        var scenarios = new[]
        {
            new ShareMetricScenario("medium", 210_053, 19_953, 266),
            new ShareMetricScenario("large", 9_999_999, 999_999, 99_999),
            new ShareMetricScenario("xlarge", 99_999_999, 9_999_999, 999_999),
        };

        foreach (var scenario in scenarios)
        {
            var window = new AppMainWindow();
            var viewModel = CreateMainWindowViewModel(
                new StubProjectAnalyzer(CreateShareSnapshot(scenario)),
                selectedFolderPath: stressRepoPath);
            window.DataContext = viewModel;

            try
            {
                window.Show();
                await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
                viewModel.OpenShareSnapshotCommand.Execute(null);
                window.UpdateLayout();

                AssertTextFitsHost<TextBlock, Grid>(window, "ShareTokenValueText", "ShareTokenValueHost", minRenderedHeight: 28, scenario.Name);
                AssertTextFitsHost<TextBlock, Grid>(window, "ShareLineValueText", "ShareLineValueHost", minRenderedHeight: 12, scenario.Name);
                AssertTextFitsHost<TextBlock, Grid>(window, "ShareFileValueText", "ShareFileValueHost", minRenderedHeight: 12, scenario.Name);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task ShareSnapshotModal_SecondaryMetricValues_UseMatchingRenderedHeight_WhenCompressed()
    {
        var stressRepoPath = GetTestFolderPath("StressRepo");
        var window = new AppMainWindow();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateShareSnapshot(new ShareMetricScenario("compressed", 2_500_000_000L, 25_000_000, 999_000))),
            selectedFolderPath: stressRepoPath);
        window.DataContext = viewModel;

        try
        {
            window.Show();
            await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
            viewModel.OpenShareSnapshotCommand.Execute(null);
            window.UpdateLayout();

            var lineBounds = GetRenderedBounds<TextBlock, Grid>(window, "ShareLineValueText", "ShareLineValueHost");
            var fileBounds = GetRenderedBounds<TextBlock, Grid>(window, "ShareFileValueText", "ShareFileValueHost");

            Assert.True(Math.Abs(lineBounds.Height - fileBounds.Height) <= 0.5,
                $"Expected compressed secondary metrics to use matching rendered heights, got lines={lineBounds.Height:F2}, files={fileBounds.Height:F2}.");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertTextFitsHost<TText, THost>(Window window, string textName, string hostName, double minRenderedHeight, string scenarioName)
        where TText : Control
        where THost : Control
    {
        var renderedBounds = GetRenderedBounds<TText, THost>(window, textName, hostName);
        var host = FindNamedDescendant<THost>(window, hostName);

        Assert.NotNull(host);
        var hostBounds = new Rect(host.Bounds.Size);

        Assert.True(renderedBounds.Width > 0, $"Expected {textName} to render with positive width for scenario '{scenarioName}', got {renderedBounds}.");
        Assert.True(renderedBounds.Height >= minRenderedHeight, $"Expected {textName} to stay readable for scenario '{scenarioName}', got rendered bounds {renderedBounds}.");
        Assert.True(renderedBounds.X >= -0.5, $"Expected {textName} to stay within {hostName} for scenario '{scenarioName}', got {renderedBounds} inside host {hostBounds}.");
        Assert.True(renderedBounds.Y >= -0.5, $"Expected {textName} to stay within {hostName} for scenario '{scenarioName}', got {renderedBounds} inside host {hostBounds}.");
        Assert.True(renderedBounds.Right <= hostBounds.Right + 0.5, $"Expected {textName} to stay within {hostName} for scenario '{scenarioName}', got {renderedBounds} inside host {hostBounds}.");
        Assert.True(renderedBounds.Bottom <= hostBounds.Bottom + 0.5, $"Expected {textName} to stay within {hostName} for scenario '{scenarioName}', got {renderedBounds} inside host {hostBounds}.");
    }

    private static Rect GetRenderedBounds<TText, THost>(Window window, string textName, string hostName)
        where TText : Control
        where THost : Control
    {
        var text = FindNamedDescendant<TText>(window, textName);
        var host = FindNamedDescendant<THost>(window, hostName);

        Assert.NotNull(text);
        Assert.NotNull(host);

        var transform = text.TransformToVisual(host);
        Assert.NotNull(transform);

        return new Rect(text.Bounds.Size).TransformToAABB(transform.Value);
    }

    private static ProjectSnapshot CreateShareSnapshot(ShareMetricScenario scenario)
    {
        var stressRepoPath = GetTestFolderPath("StressRepo");
        var childTokens = Math.Max(1L, scenario.Tokens / 2);
        var remainingTokens = Math.Max(1L, scenario.Tokens - childTokens);
        var childLines = Math.Max(1, scenario.NonEmptyLines / 2);
        var remainingLines = Math.Max(1, scenario.NonEmptyLines - childLines);
        var fileCount = Math.Max(1, scenario.DescendantFileCount);

        return new ProjectSnapshot
        {
            RootPath = stressRepoPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "StressRepo",
                FullPath = stressRepoPath,
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: fileCount, descendantDirectoryCount: 0),
                ComputedMetrics = MetricTestData.CreateComputedMetrics(
                    tokens: scenario.Tokens,
                    nonEmptyLines: scenario.NonEmptyLines,
                    fileSizeBytes: scenario.Tokens * 8L),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "src",
                        Name = "src",
                        FullPath = Path.Combine(stressRepoPath, "src"),
                        RelativePath = "src",
                        Kind = ProjectNodeKind.File,
                        Summary = MetricTestData.CreateFileSummary(),
                        ComputedMetrics = MetricTestData.CreateComputedMetrics(
                            tokens: childTokens,
                            nonEmptyLines: childLines,
                            fileSizeBytes: childTokens * 8L),
                    },
                    new ProjectNode
                    {
                        Id = "tests",
                        Name = "tests",
                        FullPath = Path.Combine(stressRepoPath, "tests"),
                        RelativePath = "tests",
                        Kind = ProjectNodeKind.File,
                        Summary = MetricTestData.CreateFileSummary(),
                        ComputedMetrics = MetricTestData.CreateComputedMetrics(
                            tokens: remainingTokens,
                            nonEmptyLines: remainingLines,
                            fileSizeBytes: remainingTokens * 8L),
                    },
                },
            },
        };
    }

    private sealed record ShareMetricScenario(string Name, long Tokens, int NonEmptyLines, int DescendantFileCount);
}
