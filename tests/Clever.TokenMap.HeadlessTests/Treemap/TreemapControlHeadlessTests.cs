using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Treemap;
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
        Assert.Contains("Non-empty/Blank: 11/1", control.TooltipText);
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
    public void TreemapControl_Hover_UsesSelectedMetricForShare()
    {
        var snapshot = CreateMetricSensitiveSnapshot();
        var control = CreateControl(snapshot, AnalysisMetric.TotalLines);
        var window = CreateHostWindow(control);

        window.Show();

        var visual = Assert.Single(control.NodeVisuals, item => item.Node.RelativePath == "a.cs");
        control.UpdateHover(GetCenter(visual));

        Assert.Contains("Share: 20.0%", control.TooltipText);
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

        Assert.Equal("a.cs", largestByTokens.Node.RelativePath);
        Assert.Equal("b.cs", largestByLines.Node.RelativePath);
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
                TotalLines: 12,
                NonEmptyLines: 11,
                BlankLines: 1,
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
                TotalLines: 12,
                NonEmptyLines: 11,
                BlankLines: 1,
                FileSizeBytes: 128,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        });

        return snapshot;
    }

    private static Clever.TokenMap.Core.Models.ProjectSnapshot CreateMetricSensitiveSnapshot()
    {
        var snapshot = CreateSnapshot();
        snapshot.Root.Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
            Tokens: 100,
            TotalLines: 100,
            NonEmptyLines: 100,
            BlankLines: 0,
            FileSizeBytes: 256,
            DescendantFileCount: 2,
            DescendantDirectoryCount: 0);

        snapshot.Root.Children.Clear();
        snapshot.Root.Children.Add(new Clever.TokenMap.Core.Models.ProjectNode
        {
            Id = "a.cs",
            Name = "a.cs",
            FullPath = "C:\\Demo\\a.cs",
            RelativePath = "a.cs",
            Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
            Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                Tokens: 80,
                TotalLines: 20,
                NonEmptyLines: 10,
                BlankLines: 10,
                FileSizeBytes: 128,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        });
        snapshot.Root.Children.Add(new Clever.TokenMap.Core.Models.ProjectNode
        {
            Id = "b.cs",
            Name = "b.cs",
            FullPath = "C:\\Demo\\b.cs",
            RelativePath = "b.cs",
            Kind = Clever.TokenMap.Core.Enums.ProjectNodeKind.File,
            Metrics = new Clever.TokenMap.Core.Models.NodeMetrics(
                Tokens: 20,
                TotalLines: 80,
                NonEmptyLines: 90,
                BlankLines: 0,
                FileSizeBytes: 128,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        });

        return snapshot;
    }
}
