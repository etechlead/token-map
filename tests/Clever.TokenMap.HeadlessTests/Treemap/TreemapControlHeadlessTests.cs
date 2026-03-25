using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Treemap;
using Avalonia.Media;
using static Clever.TokenMap.HeadlessTests.HeadlessTestSupport;

namespace Clever.TokenMap.HeadlessTests;

public sealed class TreemapControlHeadlessTests
{
    [AvaloniaFact]
    public void TreemapControl_RendersSnapshotWithoutChildControls()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        Assert.NotEmpty(control.NodeVisuals);
    }

    [AvaloniaFact]
    public void TreemapControl_HitTest_ReturnsRenderedNode()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        var hitNode = control.HitTestNode(point);

        Assert.NotNull(hitNode);
        Assert.Equal(visual.Node.RelativePath, hitNode.RelativePath);
        Assert.Null(control.HitTestNode(new Point(-10, -10)));
    }

    [AvaloniaFact]
    public void TreemapControl_Hover_UpdatesTooltipStateWithoutChangingSelection()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = new Point(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

        control.UpdateHover(point);

        Assert.Equal("Program.cs", control.HoveredNode?.RelativePath);
        Assert.Null(control.SelectedNode);
        Assert.Contains("Program.cs", control.TooltipText);
        Assert.Contains("Non-empty lines: 11", control.TooltipText);
        Assert.Contains("Ext: .cs", control.TooltipText);
        Assert.Equal(point, control.TooltipAnchorPoint);

        control.ClearHover();

        Assert.Null(control.HoveredNode);
        Assert.Null(control.TooltipText);
        Assert.Null(control.TooltipAnchorPoint);
    }

    [AvaloniaFact]
    public void TreemapControl_HoverWithinSameVisual_UpdatesTooltipAnchor()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var firstPoint = GetCenter(visual);
        var secondPoint = new Point(visual.Bounds.X + 2, visual.Bounds.Y + 2);

        control.UpdateHover(firstPoint);

        control.UpdateHover(secondPoint);

        Assert.Equal(secondPoint, control.TooltipAnchorPoint);
        Assert.Equal("Program.cs", control.HoveredNode?.RelativePath);
    }

    [AvaloniaFact]
    public void TreemapControl_SelectNodeAt_OutsideVisual_ClearsSelection()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        control.SelectNodeAt(GetCenter(visual));

        Assert.Equal("Program.cs", control.SelectedNode?.RelativePath);
        Assert.Equal("Program.cs", control.PressedNode?.RelativePath);

        control.SelectNodeAt(new Point(-10, -10));

        Assert.Null(control.SelectedNode);
        Assert.Null(control.PressedNode);
    }

    [Fact]
    public void TreemapControl_IsDrillDownGesture_RequiresSameMouseButton()
    {
        Assert.True(TreemapControl.IsDrillDownGesture(PointerType.Mouse, 2, MouseButton.Left, MouseButton.Left));
        Assert.False(TreemapControl.IsDrillDownGesture(PointerType.Mouse, 2, MouseButton.Right, MouseButton.Left));
        Assert.False(TreemapControl.IsDrillDownGesture(PointerType.Mouse, 2, MouseButton.Right, null));
        Assert.True(TreemapControl.IsDrillDownGesture(PointerType.Touch, 2, null, null));
    }

    [Fact]
    public void TreemapControl_GetPressedMouseButton_MapsPressedUpdateKinds()
    {
        Assert.Equal(MouseButton.Left, TreemapControl.GetPressedMouseButton(PointerUpdateKind.LeftButtonPressed));
        Assert.Equal(MouseButton.Right, TreemapControl.GetPressedMouseButton(PointerUpdateKind.RightButtonPressed));
        Assert.Equal(MouseButton.Middle, TreemapControl.GetPressedMouseButton(PointerUpdateKind.MiddleButtonPressed));
        Assert.Equal(MouseButton.XButton1, TreemapControl.GetPressedMouseButton(PointerUpdateKind.XButton1Pressed));
        Assert.Equal(MouseButton.XButton2, TreemapControl.GetPressedMouseButton(PointerUpdateKind.XButton2Pressed));
        Assert.Null(TreemapControl.GetPressedMouseButton(PointerUpdateKind.Other));
    }

    [AvaloniaFact]
    public async Task TreemapControl_MixedMouseButtons_DoNotTriggerDrillDown()
    {
        var control = CreateControl(CreateNestedSnapshot());
        var window = CreateHostWindow(control);
        var drillDownRequests = 0;
        control.DrillDownRequested += (_, _) => drillDownRequests++;

        window.Show();
        await WaitForUiAsync(window);

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        var point = TranslateToWindow(window, control, new Point(directoryVisual.Bounds.X + 6, directoryVisual.Bounds.Y + 6));

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Right, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Right, RawInputModifiers.None);
        await WaitForUiAsync(window);

        Assert.Equal(0, drillDownRequests);
        Assert.Equal("/", control.RootNode?.Id);
        Assert.Equal("src", control.SelectedNode?.RelativePath);
    }

    [AvaloniaFact]
    public void TreemapControl_RequestDrillDownAt_File_ReturnsFalseWithoutRaisingEvent()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);
        var drillDownRequests = 0;
        control.DrillDownRequested += (_, _) => drillDownRequests++;

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        control.SelectedNode = visual.Node;

        var handled = control.RequestDrillDownAt(GetCenter(visual));

        Assert.False(handled);
        Assert.Equal(0, drillDownRequests);
        Assert.Equal("Program.cs", control.SelectedNode?.RelativePath);
        Assert.Null(control.PressedNode);
    }

    [AvaloniaFact]
    public void TreemapControl_RequestDrillDownAt_EmptyDirectory_ReturnsFalseWithoutRaisingEvent()
    {
        var control = CreateControl(CreateSnapshotWithEmptyDirectory());
        var window = CreateHostWindow(control);
        var drillDownRequests = 0;
        control.DrillDownRequested += (_, _) => drillDownRequests++;

        window.Show();

        var directoryVisual = Assert.Single(control.NodeVisuals);
        var handled = control.RequestDrillDownAt(GetCenter(directoryVisual));

        Assert.False(handled);
        Assert.Equal(0, drillDownRequests);
        Assert.Null(control.SelectedNode);
        Assert.Null(control.PressedNode);
    }

    [AvaloniaFact]
    public void TreemapControl_UpdateHover_OutsideVisual_ClearsTooltipState()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        control.UpdateHover(GetCenter(visual));
        Assert.NotNull(control.HoveredNode);

        control.UpdateHover(new Point(-10, -10));

        Assert.Null(control.HoveredNode);
        Assert.Null(control.TooltipText);
        Assert.Null(control.TooltipAnchorPoint);
    }

    [AvaloniaFact]
    public void TreemapControl_SetTooltipSuppressed_HidesTooltipAndBlocksHoverUntilReenabled()
    {
        var control = CreateControl();
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        var point = GetCenter(visual);

        control.UpdateHover(point);

        Assert.NotNull(control.TooltipText);
        Assert.Equal("Program.cs", control.HoveredNode?.RelativePath);

        control.SetTooltipSuppressed(true);

        Assert.True(control.IsTooltipSuppressed);
        Assert.Null(control.HoveredNode);
        Assert.Null(control.TooltipText);
        Assert.Null(control.TooltipAnchorPoint);

        control.UpdateHover(point);

        Assert.Null(control.HoveredNode);
        Assert.Null(control.TooltipText);

        control.SetTooltipSuppressed(false);
        control.UpdateHover(point);

        Assert.False(control.IsTooltipSuppressed);
        Assert.Equal("Program.cs", control.HoveredNode?.RelativePath);
        Assert.NotNull(control.TooltipText);
    }

    [AvaloniaFact]
    public void TreemapControl_HoverDirectory_UsesDirectoryTooltipFields()
    {
        var control = CreateControl(CreateNestedSnapshot());
        var window = CreateHostWindow(control);

        window.Show();

        var directoryVisual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "src");
        control.UpdateHover(new Point(directoryVisual.Bounds.X + 6, directoryVisual.Bounds.Y + 6));

        Assert.Equal("src", control.HoveredNode?.RelativePath);
        Assert.Contains("Directory", control.TooltipText);
        Assert.Contains("Ext: n/a", control.TooltipText);
        Assert.Contains("Files in subtree: 1", control.TooltipText);
    }

    [AvaloniaFact]
    public void TreemapControl_HoverFileWithoutExtension_ShowsNoneExtension()
    {
        var control = CreateControl(CreateSnapshotWithFileWithoutExtension());
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        control.UpdateHover(GetCenter(visual));

        Assert.Contains("LICENSE", control.TooltipText);
        Assert.Contains("Ext: (none)", control.TooltipText);
    }

    [AvaloniaFact]
    public void TreemapControl_HoverSkippedBinaryFile_ShowsNaForAnalysisMetrics()
    {
        var control = CreateControl(CreateSnapshotWithSkippedBinaryFile(), AnalysisMetric.Size);
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals);
        control.UpdateHover(GetCenter(visual));

        Assert.Contains("image.ico", control.TooltipText);
        Assert.Contains("Tokens: n/a", control.TooltipText);
        Assert.Contains("Non-empty lines: n/a", control.TooltipText);
        Assert.Contains("Share: 100.0%", control.TooltipText);
    }

    [AvaloniaFact]
    public void TreemapControl_Hover_UsesSelectedMetricForShare()
    {
        var snapshot = CreateMetricSensitiveSnapshot();
        var control = CreateControl(snapshot, AnalysisMetric.TotalLines);
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "a.cs");
        control.UpdateHover(GetCenter(visual));

        Assert.Contains("Share: 10.0%", control.TooltipText);
    }

    [AvaloniaFact]
    public void TreemapControl_ChangingMetric_RecomputesVisuals()
    {
        var snapshot = CreateMetricSensitiveSnapshot();
        var control = CreateControl(snapshot, AnalysisMetric.Tokens);
        var window = CreateHostWindow(control);

        window.Show();

        var largestByTokens = control.NodeVisuals
            .OrderByDescending(item => item.Bounds.Width * item.Bounds.Height)
            .First();

        control.Metric = AnalysisMetric.TotalLines;

        var largestByLines = control.NodeVisuals
            .OrderByDescending(item => item.Bounds.Width * item.Bounds.Height)
            .First();

        control.Metric = AnalysisMetric.Size;

        var largestBySize = control.NodeVisuals
            .OrderByDescending(item => item.Bounds.Width * item.Bounds.Height)
            .First();

        Assert.Equal("a.cs", largestByTokens.Node.RelativePath);
        Assert.Equal("b.cs", largestByLines.Node.RelativePath);
        Assert.Equal("c.cs", largestBySize.Node.RelativePath);
    }

    [AvaloniaFact]
    public void TreemapControl_LoadsStyledThemeBrushes()
    {
        var control = CreateControl(CreateNestedSnapshot());
        var window = CreateHostWindow(control);

        window.Show();

        var directoryBorderBrush = Assert.IsType<SolidColorBrush>(control.DirectoryBorderBrush);
        var canvasBackgroundBrush = Assert.IsType<SolidColorBrush>(control.CanvasBackgroundBrush);
        var leafBorderBrush = Assert.IsType<SolidColorBrush>(control.LeafBorderBrush);
        var hoverBorderBrush = Assert.IsType<SolidColorBrush>(control.HoverBorderBrush);

        Assert.True(directoryBorderBrush.Color.A > 0, "Directory border brush should be themed, not transparent.");
        Assert.True(canvasBackgroundBrush.Color.A > 0, "Canvas background brush should be themed, not transparent.");
        Assert.True(leafBorderBrush.Color.A > 0, "Leaf border brush should be themed, not transparent.");
        Assert.True(hoverBorderBrush.Color.A > 0, "Hover border brush should be themed, not transparent.");
    }

    private static TreemapControl CreateControl(
        Clever.TokenMap.Core.Models.ProjectSnapshot? snapshot = null,
        AnalysisMetric metric = AnalysisMetric.Tokens) =>
        new()
        {
            Width = 320,
            Height = 180,
            RootNode = (snapshot ?? CreateSnapshot()).Root,
            Metric = metric,
        };

    private static Window CreateHostWindow(TreemapControl control) =>
        new()
        {
            Content = control,
            Width = 360,
            Height = 240,
        };

    private static Point GetCenter(TreemapNodeVisual visual) =>
        new(
            visual.Bounds.X + (visual.Bounds.Width / 2),
            visual.Bounds.Y + (visual.Bounds.Height / 2));

    private static Point TranslateToWindow(Window window, Control control, Point point)
    {
        return control.TranslatePoint(point, window)
               ?? throw new InvalidOperationException("Unable to translate treemap point to window coordinates.");
    }

    private static async Task WaitForUiAsync(Window window)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Loaded);
        window.UpdateLayout();
    }

    private static Clever.TokenMap.Core.Models.ProjectSnapshot CreateSnapshotWithEmptyDirectory()
    {
        var snapshot = CreateSnapshot();
        snapshot.Root.Children.Clear();
        snapshot.Root.Children.Add(new Clever.TokenMap.Core.Models.ProjectNode
        {
            Id = "src",
            Name = "src",
            FullPath = "C:\\Demo\\src",
            RelativePath = "src",
            Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.Directory,
            Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                Tokens: 42,
                NonEmptyLines: 11,
                FileSizeBytes: 128,
                DescendantFileCount: 0,
                DescendantDirectoryCount: 0),
        });

        return snapshot;
    }

    private static Clever.TokenMap.Core.Models.ProjectSnapshot CreateSnapshotWithFileWithoutExtension()
    {
        var snapshot = CreateSnapshot();
        snapshot.Root.Children.Clear();
        snapshot.Root.Children.Add(new Clever.TokenMap.Core.Models.ProjectNode
        {
            Id = "LICENSE",
            Name = "LICENSE",
            FullPath = "C:\\Demo\\LICENSE",
            RelativePath = "LICENSE",
            Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
            Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                Tokens: 42,
                NonEmptyLines: 11,
                FileSizeBytes: 128,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        });

        return snapshot;
    }

    private static Clever.TokenMap.Core.Models.ProjectSnapshot CreateSnapshotWithSkippedBinaryFile()
    {
        return new Clever.TokenMap.Core.Models.ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = Clever.TokenMap.Core.Models.ScanOptions.Default,
            Root = new Clever.TokenMap.Core.Models.ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.Root,
                Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                    Tokens: 0,
                    NonEmptyLines: 0,
                    FileSizeBytes: 171_801,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
                Children =
                {
                    new Clever.TokenMap.Core.Models.ProjectNode
                    {
                        Id = "image.ico",
                        Name = "image.ico",
                        FullPath = "C:\\Demo\\image.ico",
                        RelativePath = "image.ico",
                        Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
                        SkippedReason = SkippedReason.Binary,
                        Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                            Tokens: 0,
                            NonEmptyLines: 0,
                            FileSizeBytes: 171_801,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            },
        };
    }

    private static Clever.TokenMap.Core.Models.ProjectSnapshot CreateMetricSensitiveSnapshot()
    {
        return new Clever.TokenMap.Core.Models.ProjectSnapshot
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = Clever.TokenMap.Core.Models.ScanOptions.Default,
            Root = new Clever.TokenMap.Core.Models.ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.Root,
                Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                    Tokens: 105,
                    NonEmptyLines: 100,
                    FileSizeBytes: 350,
                    DescendantFileCount: 3,
                    DescendantDirectoryCount: 0),
                Children =
                {
                    new Clever.TokenMap.Core.Models.ProjectNode
                    {
                        Id = "a.cs",
                        Name = "a.cs",
                        FullPath = "C:\\Demo\\a.cs",
                        RelativePath = "a.cs",
                        Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
                        Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                            Tokens: 80,
                            NonEmptyLines: 10,
                            FileSizeBytes: 50,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                    new Clever.TokenMap.Core.Models.ProjectNode
                    {
                        Id = "b.cs",
                        Name = "b.cs",
                        FullPath = "C:\\Demo\\b.cs",
                        RelativePath = "b.cs",
                        Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
                        Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                            Tokens: 20,
                            NonEmptyLines: 90,
                            FileSizeBytes: 75,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                    new Clever.TokenMap.Core.Models.ProjectNode
                    {
                        Id = "c.cs",
                        Name = "c.cs",
                        FullPath = "C:\\Demo\\c.cs",
                        RelativePath = "c.cs",
                        Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
                        Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                            Tokens: 5,
                            NonEmptyLines: 0,
                            FileSizeBytes: 225,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            },
        };
    }
}
