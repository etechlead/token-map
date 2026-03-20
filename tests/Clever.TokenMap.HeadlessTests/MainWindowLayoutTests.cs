using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.Controls;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

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
        Assert.NotNull(window.FindControl<TreemapControl>("ProjectTreemapControl"));
    }

    [AvaloniaFact]
    public async Task MainWindow_OpenFolderFlow_PopulatesTreeAndDetails()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        await viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);

        var treeView = window.FindControl<TreeView>("ProjectTreeControl");
        var statusText = window.FindControl<TextBlock>("StatusValueText");
        var detailsPathText = window.FindControl<TextBlock>("DetailsPathText");

        Assert.NotNull(treeView);
        Assert.Single(viewModel.Tree.RootNodes);
        Assert.Equal("Completed", statusText?.Text);
        Assert.Equal("Path: (root)", detailsPathText?.Text);
    }

    [AvaloniaFact]
    public async Task MainWindow_CancelCommand_UpdatesStatus()
    {
        var window = new MainWindow();
        var viewModel = new MainWindowViewModel(
            new CancelAwareProjectAnalyzer(),
            new StubFolderPickerService("C:\\Demo"));
        window.DataContext = viewModel;

        window.Show();
        var openTask = viewModel.Toolbar.OpenFolderCommand.ExecuteAsync(null);
        await Task.Delay(100);
        viewModel.Toolbar.CancelCommand.Execute(null);
        await openTask;

        var statusText = window.FindControl<TextBlock>("StatusValueText");
        Assert.Equal("Cancelled", statusText?.Text);
    }

    [AvaloniaFact]
    public void TreemapControl_RendersSnapshotWithoutChildControls()
    {
        var control = new TreemapControl
        {
            Width = 320,
            Height = 180,
            RootNode = CreateSnapshot().Root,
            Metric = "Tokens",
        };
        var window = new Window
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

        window.Show();

        Assert.NotEmpty(control.NodeVisuals);
    }

    [AvaloniaFact]
    public void TreemapControl_HitTest_ReturnsRenderedNode()
    {
        var control = new TreemapControl
        {
            Width = 320,
            Height = 180,
            RootNode = CreateSnapshot().Root,
            Metric = "Tokens",
        };
        var window = new Window
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Avalonia.Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        var hitNode = control.HitTestNode(point);

        Assert.NotNull(hitNode);
        Assert.Equal(visual.Node.RelativePath, hitNode.RelativePath);
        Assert.Null(control.HitTestNode(new Avalonia.Point(-10, -10)));
    }

    private static ProjectSnapshot CreateSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    TotalLines: 12,
                    CodeLines: 10,
                    CommentLines: 1,
                    BlankLines: 1,
                    Language: null,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "Program.cs",
                        Name = "Program.cs",
                        FullPath = "C:\\Demo\\Program.cs",
                        RelativePath = "Program.cs",
                        Kind = ProjectNodeKind.File,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            TotalLines: 12,
                            CodeLines: 10,
                            CommentLines: 1,
                            BlankLines: 1,
                            Language: "C#",
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            },
        };

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult(path);
    }

    private sealed class StubProjectAnalyzer(ProjectSnapshot snapshot) : IProjectAnalyzer
    {
        public Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class CancelAwareProjectAnalyzer : IProjectAnalyzer
    {
        public async Task<ProjectSnapshot> AnalyzeAsync(
            string rootPath,
            ScanOptions options,
            IProgress<AnalysisProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "Program.cs"));
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            throw new InvalidOperationException("This path should have been cancelled.");
        }
    }
}
