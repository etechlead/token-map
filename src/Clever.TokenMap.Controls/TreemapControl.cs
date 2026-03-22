using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Clever.TokenMap.Controls.Layout;
using Clever.TokenMap.Controls.Models;
using Clever.TokenMap.Core.Models;
using System.IO;

namespace Clever.TokenMap.Controls;

public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<string> MetricProperty =
        AvaloniaProperty.Register<TreemapControl, string>(nameof(Metric), "Tokens");

    public static readonly StyledProperty<ProjectNode?> RootNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(RootNode));

    public static readonly StyledProperty<ProjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(SelectedNode), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly SquarifiedTreemapLayout _layout = new();
    private IReadOnlyList<TreemapNodeVisual> _nodeVisuals = [];
    private Size _layoutSize;

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(MetricProperty, RootNodeProperty, BoundsProperty);
    }

    public string Metric
    {
        get => GetValue(MetricProperty);
        set => SetValue(MetricProperty, value);
    }

    public ProjectNode? RootNode
    {
        get => GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public ProjectNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    internal ProjectNode? HoveredNode { get; private set; }

    internal ProjectNode? PressedNode { get; private set; }

    internal string? TooltipText { get; private set; }

    internal IReadOnlyList<TreemapNodeVisual> NodeVisuals => _nodeVisuals;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arrangedSize = base.ArrangeOverride(finalSize);
        UpdateVisuals(arrangedSize);
        return arrangedSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MetricProperty || change.Property == RootNodeProperty)
        {
            UpdateVisuals(Bounds.Size);
            InvalidateVisual();
        }

        if (change.Property == SelectedNodeProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var drawingBounds = new Rect(Bounds.Size).Deflate(6);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0B1018")), new Rect(Bounds.Size));

        if (_layoutSize != Bounds.Size)
        {
            UpdateVisuals(Bounds.Size);
        }

        if (RootNode is null || drawingBounds.Width <= 0 || drawingBounds.Height <= 0)
        {
            DrawPlaceholder(context, "Run analysis to populate treemap.");
            return;
        }

        if (_nodeVisuals.Count == 0)
        {
            DrawPlaceholder(context, "No weighted nodes for the selected metric.");
            return;
        }

        foreach (var visual in _nodeVisuals)
        {
            var isLeaf = IsLeafNode(visual.Node);
            if (isLeaf)
            {
                var fill = new SolidColorBrush(TreemapColorRules.GetLeafColor(visual.Node));
                context.FillRectangle(fill, visual.Bounds);
            }
            else if (visual.Bounds.Width >= 12 && visual.Bounds.Height >= 12)
            {
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                    visual.Bounds);

                var headerBounds = TreemapVisualRules.GetHeaderBounds(visual.Node, visual.Bounds);
                if (headerBounds.Height > 0)
                {
                    context.FillRectangle(
                        new SolidColorBrush(Color.FromArgb(76, 255, 255, 255)),
                        headerBounds);
                }
            }

            context.DrawRectangle(CreateBorderPen(visual.Node), visual.Bounds);

            if (TreemapVisualRules.CanDrawLabel(visual.Node, visual.Bounds))
            {
                var labelBounds = TreemapVisualRules.GetLabelBounds(visual.Node, visual.Bounds);
                var formattedText = new FormattedText(
                    visual.Node.Name,
                    culture: System.Globalization.CultureInfo.InvariantCulture,
                    flowDirection: FlowDirection.LeftToRight,
                    typeface: Typeface.Default,
                    emSize: TreemapVisualRules.GetLabelFontSize(visual.Node),
                    foreground: Brushes.White);

                using var clip = context.PushClip(labelBounds);
                context.DrawText(formattedText, new Point(labelBounds.X, labelBounds.Y));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        UpdateHover(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SelectNodeAt(e.GetPosition(this));
    }

    private void DrawPlaceholder(DrawingContext context, string message)
    {
        var formattedText = new FormattedText(
            message,
            culture: System.Globalization.CultureInfo.InvariantCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: 12,
            foreground: new SolidColorBrush(Color.Parse("#8FA3B8")));

        var point = new Point(
            Math.Max(12, (Bounds.Width - formattedText.Width) / 2),
            Math.Max(12, (Bounds.Height - formattedText.Height) / 2));
        context.DrawText(formattedText, point);
    }

    private void UpdateVisuals(Size availableSize)
    {
        _layoutSize = availableSize;

        var drawingBounds = new Rect(availableSize).Deflate(6);
        if (RootNode is null || drawingBounds.Width <= 0 || drawingBounds.Height <= 0)
        {
            _nodeVisuals = [];
            return;
        }

        _nodeVisuals = _layout.Calculate(RootNode, drawingBounds, Metric);
    }

    internal ProjectNode? HitTestNode(Point point)
    {
        for (var index = _nodeVisuals.Count - 1; index >= 0; index--)
        {
            if (_nodeVisuals[index].Bounds.Contains(point))
            {
                return _nodeVisuals[index].Node;
            }
        }

        return null;
    }

    internal void UpdateHover(Point point)
    {
        var hoveredNode = HitTestNode(point);
        if (ReferenceEquals(HoveredNode, hoveredNode))
        {
            return;
        }

        HoveredNode = hoveredNode;
        TooltipText = hoveredNode is null ? null : BuildTooltip(hoveredNode);
        ToolTip.SetTip(this, TooltipText);
        ToolTip.SetShowDelay(this, 0);
        ToolTip.SetIsOpen(this, hoveredNode is not null);
        InvalidateVisual();
    }

    internal void ClearHover()
    {
        if (HoveredNode is null && TooltipText is null)
        {
            return;
        }

        HoveredNode = null;
        TooltipText = null;
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }

    internal void SelectNodeAt(Point point)
    {
        PressedNode = HitTestNode(point);
        SelectedNode = PressedNode;
    }

    private Pen CreateBorderPen(ProjectNode node)
    {
        var isSelected = SelectedNode?.Id == node.Id;
        var isHovered = HoveredNode?.Id == node.Id;
        var isLeaf = IsLeafNode(node);

        if (isSelected)
        {
            return new Pen(new SolidColorBrush(Color.Parse("#FFF0B3")), 2);
        }

        if (isHovered)
        {
            return new Pen(new SolidColorBrush(Color.Parse("#B7D7FF")), 2);
        }

        return isLeaf
            ? new Pen(new SolidColorBrush(Color.Parse("#121A25")), 1)
            : new Pen(new SolidColorBrush(Color.Parse("#314459")), 1);
    }

    private string BuildTooltip(ProjectNode node)
    {
        var relativePath = string.IsNullOrWhiteSpace(node.RelativePath) ? "(root)" : node.RelativePath;
        var share = RootNode is null
            ? "n/a"
            : FormatShare(GetMetricValue(node), GetMetricValue(RootNode));
        var breakdown = node.Metrics.CodeLines is null && node.Metrics.CommentLines is null && node.Metrics.BlankLines is null
            ? "n/a"
            : $"{node.Metrics.CodeLines ?? 0:N0}/{node.Metrics.CommentLines ?? 0:N0}/{node.Metrics.BlankLines ?? 0:N0}";
        var languageOrExtension = node.Metrics.Language
            ?? (node.Kind == Core.Enums.ProjectNodeKind.File
                ? Path.GetExtension(node.Name) is { Length: > 0 } extension ? extension : "(none)"
                : "n/a");

        return $"{relativePath}\n{GetKindText(node)}\nTokens: {node.Metrics.Tokens:N0}\nShare: {share}\nLines: {node.Metrics.TotalLines:N0}\nCode/Comments/Blanks: {breakdown}\nLanguage/Ext: {languageOrExtension}\nFiles in subtree: {node.Metrics.DescendantFileCount:N0}";
    }

    private double GetMetricValue(ProjectNode node) =>
        Metric switch
        {
            "Total lines" => node.Metrics.TotalLines,
            "Code lines" => node.Metrics.CodeLines ?? 0,
            _ => node.Metrics.Tokens,
        };

    private static string FormatShare(double current, double total) =>
        total <= 0 || current <= 0 ? "n/a" : $"{current / total:P1}";

    private static string GetKindText(ProjectNode node) =>
        node.Kind switch
        {
            Core.Enums.ProjectNodeKind.Root => "Root",
            Core.Enums.ProjectNodeKind.Directory => "Directory",
            _ => "File",
        };

    private static bool IsLeafNode(ProjectNode node) =>
        node.Kind == Core.Enums.ProjectNodeKind.File || node.Children.Count == 0;

}
