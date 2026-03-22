using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Clever.TokenMap.Treemap.Layout;
using Clever.TokenMap.Treemap.Models;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using System.IO;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<AnalysisMetric> MetricProperty =
        AvaloniaProperty.Register<TreemapControl, AnalysisMetric>(nameof(Metric), AnalysisMetric.Tokens);

    public static readonly StyledProperty<ProjectNode?> RootNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(RootNode));

    public static readonly StyledProperty<ProjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(SelectedNode), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly SquarifiedTreemapLayout _layout = new();
    private IReadOnlyList<TreemapNodeVisual> _nodeVisuals = [];
    private Size _layoutSize;
    private static readonly IBrush SelectedAccentFallbackBrush = new SolidColorBrush(Color.Parse("#E7F2FF"));
    private static readonly IBrush HoverAccentFallbackBrush = new SolidColorBrush(Color.Parse("#8BC3FF"));
    private static readonly IBrush TooltipBorderFallbackBrush = new SolidColorBrush(Color.Parse("#D7DCE2"));
    private static readonly IBrush TooltipBackgroundFallbackBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush TooltipLabelFallbackBrush = new SolidColorBrush(Color.Parse("#667085"));
    private static readonly IBrush TooltipValueFallbackBrush = new SolidColorBrush(Color.Parse("#1F2933"));

    public event EventHandler<TreemapDrillDownRequestedEventArgs>? DrillDownRequested;

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(MetricProperty, RootNodeProperty, BoundsProperty);
    }

    public TreemapControl()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public AnalysisMetric Metric
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

        var isDarkTheme = IsDarkTheme();
        var drawingBounds = new Rect(Bounds.Size).Deflate(6);
        var canvasBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#1B2129"))
            : new SolidColorBrush(Color.Parse("#F7F8FA"));
        var directoryFillBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#242B34"))
            : new SolidColorBrush(Color.Parse("#F1F3F5"));
        var directoryHeaderBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#313845"))
            : new SolidColorBrush(Color.Parse("#E7EBF0"));
        var directoryLabelBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#F3F4F6"))
            : new SolidColorBrush(Color.Parse("#1F2933"));

        context.FillRectangle(canvasBrush, new Rect(Bounds.Size));

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
                context.FillRectangle(directoryFillBrush, visual.Bounds);

                var headerBounds = TreemapVisualRules.GetHeaderBounds(visual.Node, visual.Bounds);
                if (headerBounds.Height > 0)
                {
                    context.FillRectangle(directoryHeaderBrush, headerBounds);
                }
            }

            context.DrawRectangle(CreateBorderPen(visual.Node), visual.Bounds);

            if (TreemapVisualRules.CanDrawLabel(visual.Node, visual.Bounds))
            {
                var labelBounds = TreemapVisualRules.GetLabelBounds(visual.Node, visual.Bounds);
                IBrush labelBrush = isLeaf ? Brushes.White : directoryLabelBrush;
                var formattedText = new FormattedText(
                    visual.Node.Name,
                    culture: System.Globalization.CultureInfo.InvariantCulture,
                    flowDirection: FlowDirection.LeftToRight,
                    typeface: Typeface.Default,
                    emSize: TreemapVisualRules.GetLabelFontSize(visual.Node),
                    foreground: labelBrush);

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
        var point = e.GetPosition(this);
        SelectNodeAt(point);

        if (e.ClickCount == 2)
        {
            RequestDrillDownAt(point);
        }
    }

    private void DrawPlaceholder(DrawingContext context, string message)
    {
        var formattedText = new FormattedText(
            message,
            culture: System.Globalization.CultureInfo.InvariantCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: 12,
            foreground: IsDarkTheme()
                ? new SolidColorBrush(Color.Parse("#AAB4C0"))
                : new SolidColorBrush(Color.Parse("#667085")));

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
        ToolTip.SetTip(this, hoveredNode is null ? null : BuildTooltipContent(hoveredNode));
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

    internal bool RequestDrillDownAt(Point point)
    {
        var node = HitTestNode(point);
        if (!CanDrillDown(node))
        {
            return false;
        }

        var targetNode = node!;
        PressedNode = targetNode;
        SelectedNode = targetNode;
        DrillDownRequested?.Invoke(this, new TreemapDrillDownRequestedEventArgs(targetNode));
        return true;
    }

    private Pen CreateBorderPen(ProjectNode node)
    {
        var isDarkTheme = IsDarkTheme();
        var isSelected = SelectedNode?.Id == node.Id;
        var isHovered = HoveredNode?.Id == node.Id;
        var isLeaf = IsLeafNode(node);

        if (isSelected)
        {
            return new Pen(GetThemeBrush("TokenMapAccentStrongBrush", SelectedAccentFallbackBrush), 2);
        }

        if (isHovered)
        {
            return new Pen(GetThemeBrush("TokenMapAccentHoverBrush", HoverAccentFallbackBrush), 2);
        }

        return isLeaf
            ? new Pen(
                isDarkTheme
                    ? new SolidColorBrush(Color.FromArgb(166, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(166, 255, 255, 255)),
                1)
            : new Pen(
                isDarkTheme
                    ? new SolidColorBrush(Color.Parse("#3A4350"))
                    : new SolidColorBrush(Color.Parse("#D7DCE2")),
                1);
    }

    private IBrush GetThemeBrush(string resourceKey, IBrush fallbackBrush)
    {
        return TryGetResource(resourceKey, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : fallbackBrush;
    }

    private string BuildTooltip(ProjectNode node)
    {
        var relativePath = string.IsNullOrWhiteSpace(node.RelativePath) ? "(root)" : node.RelativePath;
        var share = RootNode is null
            ? "n/a"
            : FormatShare(GetMetricValue(node), GetMetricValue(RootNode));
        var breakdown = $"{node.Metrics.NonEmptyLines:N0}/{node.Metrics.BlankLines:N0}";
        var extension = node.Kind == Core.Enums.ProjectNodeKind.File
            ? Path.GetExtension(node.Name) is { Length: > 0 } fileExtension ? fileExtension : "(none)"
            : "n/a";

        return $"{relativePath}\n{GetKindText(node)}\nTokens: {node.Metrics.Tokens:N0}\nShare: {share}\nLines: {node.Metrics.TotalLines:N0}\nNon-empty/Blank: {breakdown}\nExt: {extension}\nFiles in subtree: {node.Metrics.DescendantFileCount:N0}";
    }

    private Border BuildTooltipContent(ProjectNode node)
    {
        var isDarkTheme = IsDarkTheme();
        var relativePath = string.IsNullOrWhiteSpace(node.RelativePath) ? "(root)" : node.RelativePath;
        var share = RootNode is null
            ? "n/a"
            : FormatShare(GetMetricValue(node), GetMetricValue(RootNode));
        var breakdown = $"{node.Metrics.NonEmptyLines:N0}/{node.Metrics.BlankLines:N0}";
        var extension = node.Kind == Core.Enums.ProjectNodeKind.File
            ? Path.GetExtension(node.Name) is { Length: > 0 } fileExtension ? fileExtension : "(none)"
            : "n/a";

        var labelBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#AAB4C0"))
            : new SolidColorBrush(Color.Parse("#667085"));
        var valueBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#F3F4F6"))
            : new SolidColorBrush(Color.Parse("#1F2933"));

        var content = new StackPanel
        {
            Spacing = 8,
        };
        content.Children.Add(new TextBlock
        {
            Text = relativePath,
            Foreground = valueBrush,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
        });
        content.Children.Add(new TextBlock
        {
            Text = GetKindText(node),
            Foreground = labelBrush,
            FontSize = 11,
        });
        content.Children.Add(CreateTooltipRow("Tokens", node.Metrics.Tokens.ToString("N0", CultureInfo.CurrentCulture), labelBrush, valueBrush));
        content.Children.Add(CreateTooltipRow("Share", share, labelBrush, valueBrush));
        content.Children.Add(CreateTooltipRow("Lines", node.Metrics.TotalLines.ToString("N0", CultureInfo.CurrentCulture), labelBrush, valueBrush));
        content.Children.Add(CreateTooltipRow("Non-empty/Blank", breakdown, labelBrush, valueBrush));
        content.Children.Add(CreateTooltipRow("Ext", extension, labelBrush, valueBrush));
        content.Children.Add(CreateTooltipRow("Files in subtree", node.Metrics.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture), labelBrush, valueBrush));

        return new Border
        {
            Background = isDarkTheme
                ? new SolidColorBrush(Color.Parse("#1C2128"))
                : TooltipBackgroundFallbackBrush,
            BorderBrush = isDarkTheme
                ? new SolidColorBrush(Color.Parse("#3A4350"))
                : TooltipBorderFallbackBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = content,
            MaxWidth = 360,
        };
    }

    private bool IsDarkTheme()
    {
        var variant = Application.Current?.ActualThemeVariant ?? ActualThemeVariant;
        return variant == ThemeVariant.Dark;
    }

    private static Grid CreateTooltipRow(string label, string value, IBrush labelBrush, IBrush valueBrush)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 10,
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = labelBrush,
            FontSize = 11,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        });

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = valueBrush,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return grid;
    }

    private double GetMetricValue(ProjectNode node) =>
        Metric switch
        {
            AnalysisMetric.TotalLines => node.Metrics.TotalLines,
            AnalysisMetric.NonEmptyLines => node.Metrics.NonEmptyLines,
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

    private static bool CanDrillDown(ProjectNode? node) =>
        node is not null &&
        node.Kind != Core.Enums.ProjectNodeKind.File &&
        node.Children.Count > 0;

}

