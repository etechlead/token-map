using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapControl : Control
{
    private const double TooltipMargin = 12;
    private const double TooltipPaddingX = 14;
    private const double TooltipPaddingY = 12;
    private const double TooltipHeaderSpacing = 10;
    private const double TooltipRowSpacing = 6;
    private const double TooltipColumnSpacing = 12;
    private const double TooltipSeparatorSpacing = 6;
    private const double TooltipSeparatorThickness = 1;
    private const double TooltipMaxWidth = 360;
    private const double InteractiveContrastThickness = 1;

    public static readonly StyledProperty<MetricId> MetricProperty =
        AvaloniaProperty.Register<TreemapControl, MetricId>(nameof(Metric), MetricIds.Tokens);

    public static readonly StyledProperty<ProjectNode?> RootNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(RootNode));

    public static readonly StyledProperty<ProjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(SelectedNode), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TreemapPalette> PaletteProperty =
        AvaloniaProperty.Register<TreemapControl, TreemapPalette>(nameof(Palette), TreemapPalette.Weighted);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<TreemapControl, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> ShowMetricValuesProperty =
        AvaloniaProperty.Register<TreemapControl, bool>(nameof(ShowMetricValues), false);

    public static readonly StyledProperty<IReadOnlyList<MetricId>> VisibleMetricIdsProperty =
        AvaloniaProperty.Register<TreemapControl, IReadOnlyList<MetricId>>(
            nameof(VisibleMetricIds),
            DefaultMetricCatalog.GetDefaultVisibleMetricIds());

    public static readonly StyledProperty<bool> ShowDirectoryNodesProperty =
        AvaloniaProperty.Register<TreemapControl, bool>(nameof(ShowDirectoryNodes), true);

    public static readonly StyledProperty<double> LeafCornerRadiusProperty =
        AvaloniaProperty.Register<TreemapControl, double>(nameof(LeafCornerRadius));

    public static readonly StyledProperty<double> LeafGapProperty =
        AvaloniaProperty.Register<TreemapControl, double>(nameof(LeafGap));

    public static readonly StyledProperty<double> MinLeafAreaRatioProperty =
        AvaloniaProperty.Register<TreemapControl, double>(nameof(MinLeafAreaRatio));

    public static readonly StyledProperty<bool> ShowLeafBordersProperty =
        AvaloniaProperty.Register<TreemapControl, bool>(nameof(ShowLeafBorders), true);

    public static readonly StyledProperty<double> CanvasInsetProperty =
        AvaloniaProperty.Register<TreemapControl, double>(nameof(CanvasInset), 6);

    public static readonly StyledProperty<IBrush> CanvasBackgroundBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(CanvasBackgroundBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> DirectoryFillBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(DirectoryFillBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> DirectoryHeaderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(DirectoryHeaderBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> DirectoryBorderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(DirectoryBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> DirectoryLabelBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(DirectoryLabelBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> LeafBorderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(LeafBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> LeafLightLabelBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(LeafLightLabelBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> LeafDarkLabelBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(LeafDarkLabelBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> PlaceholderForegroundBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(PlaceholderForegroundBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> TooltipLabelBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(TooltipLabelBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> TooltipValueBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(TooltipValueBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> TooltipBackgroundBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(TooltipBackgroundBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> TooltipBorderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(TooltipBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> SelectedBorderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(SelectedBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> HoverBorderBrushProperty =
        AvaloniaProperty.Register<TreemapControl, IBrush>(nameof(HoverBorderBrush), Brushes.Transparent);

    private readonly SquarifiedTreemapLayout _layout = new();
    private List<TreemapNodeVisual> _nodeVisuals = [];
    private bool _isTooltipSuppressed;
    private Size _layoutSize;
    private MouseButton? _lastPressedMouseButton;
    private Point? _tooltipAnchorPoint;
    private TreemapTooltipState? _tooltipState;
    public event EventHandler<TreemapDrillDownRequestedEventArgs>? DrillDownRequested;
    internal event EventHandler? TooltipStateChanged;

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(
            MetricProperty,
            RootNodeProperty,
            SelectedNodeProperty,
            PaletteProperty,
            ShowLabelsProperty,
            ShowMetricValuesProperty,
            VisibleMetricIdsProperty,
            ShowDirectoryNodesProperty,
            LeafCornerRadiusProperty,
            LeafGapProperty,
            MinLeafAreaRatioProperty,
            ShowLeafBordersProperty,
            CanvasInsetProperty,
            BoundsProperty,
            CanvasBackgroundBrushProperty,
            DirectoryFillBrushProperty,
            DirectoryHeaderBrushProperty,
            DirectoryBorderBrushProperty,
            DirectoryLabelBrushProperty,
            LeafBorderBrushProperty,
            LeafLightLabelBrushProperty,
            LeafDarkLabelBrushProperty,
            PlaceholderForegroundBrushProperty,
            TooltipLabelBrushProperty,
            TooltipValueBrushProperty,
            TooltipBackgroundBrushProperty,
            TooltipBorderBrushProperty,
            SelectedBorderBrushProperty,
            HoverBorderBrushProperty);
    }

    public MetricId Metric
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

    public TreemapPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool ShowMetricValues
    {
        get => GetValue(ShowMetricValuesProperty);
        set => SetValue(ShowMetricValuesProperty, value);
    }

    public IReadOnlyList<MetricId> VisibleMetricIds
    {
        get => GetValue(VisibleMetricIdsProperty);
        set => SetValue(VisibleMetricIdsProperty, value);
    }

    public bool ShowDirectoryNodes
    {
        get => GetValue(ShowDirectoryNodesProperty);
        set => SetValue(ShowDirectoryNodesProperty, value);
    }

    public double LeafCornerRadius
    {
        get => GetValue(LeafCornerRadiusProperty);
        set => SetValue(LeafCornerRadiusProperty, value);
    }

    public double LeafGap
    {
        get => GetValue(LeafGapProperty);
        set => SetValue(LeafGapProperty, value);
    }

    public double MinLeafAreaRatio
    {
        get => GetValue(MinLeafAreaRatioProperty);
        set => SetValue(MinLeafAreaRatioProperty, value);
    }

    public bool ShowLeafBorders
    {
        get => GetValue(ShowLeafBordersProperty);
        set => SetValue(ShowLeafBordersProperty, value);
    }

    public double CanvasInset
    {
        get => GetValue(CanvasInsetProperty);
        set => SetValue(CanvasInsetProperty, value);
    }

    public IBrush CanvasBackgroundBrush
    {
        get => GetValue(CanvasBackgroundBrushProperty);
        set => SetValue(CanvasBackgroundBrushProperty, value);
    }

    public IBrush DirectoryFillBrush
    {
        get => GetValue(DirectoryFillBrushProperty);
        set => SetValue(DirectoryFillBrushProperty, value);
    }

    public IBrush DirectoryHeaderBrush
    {
        get => GetValue(DirectoryHeaderBrushProperty);
        set => SetValue(DirectoryHeaderBrushProperty, value);
    }

    public IBrush DirectoryBorderBrush
    {
        get => GetValue(DirectoryBorderBrushProperty);
        set => SetValue(DirectoryBorderBrushProperty, value);
    }

    public IBrush DirectoryLabelBrush
    {
        get => GetValue(DirectoryLabelBrushProperty);
        set => SetValue(DirectoryLabelBrushProperty, value);
    }

    public IBrush LeafBorderBrush
    {
        get => GetValue(LeafBorderBrushProperty);
        set => SetValue(LeafBorderBrushProperty, value);
    }

    public IBrush LeafLightLabelBrush
    {
        get => GetValue(LeafLightLabelBrushProperty);
        set => SetValue(LeafLightLabelBrushProperty, value);
    }

    public IBrush LeafDarkLabelBrush
    {
        get => GetValue(LeafDarkLabelBrushProperty);
        set => SetValue(LeafDarkLabelBrushProperty, value);
    }

    public IBrush PlaceholderForegroundBrush
    {
        get => GetValue(PlaceholderForegroundBrushProperty);
        set => SetValue(PlaceholderForegroundBrushProperty, value);
    }

    public IBrush TooltipLabelBrush
    {
        get => GetValue(TooltipLabelBrushProperty);
        set => SetValue(TooltipLabelBrushProperty, value);
    }

    public IBrush TooltipValueBrush
    {
        get => GetValue(TooltipValueBrushProperty);
        set => SetValue(TooltipValueBrushProperty, value);
    }

    public IBrush TooltipBackgroundBrush
    {
        get => GetValue(TooltipBackgroundBrushProperty);
        set => SetValue(TooltipBackgroundBrushProperty, value);
    }

    public IBrush TooltipBorderBrush
    {
        get => GetValue(TooltipBorderBrushProperty);
        set => SetValue(TooltipBorderBrushProperty, value);
    }

    public IBrush SelectedBorderBrush
    {
        get => GetValue(SelectedBorderBrushProperty);
        set => SetValue(SelectedBorderBrushProperty, value);
    }

    public IBrush HoverBorderBrush
    {
        get => GetValue(HoverBorderBrushProperty);
        set => SetValue(HoverBorderBrushProperty, value);
    }

    internal ProjectNode? HoveredNode { get; private set; }

    internal ProjectNode? PressedNode { get; private set; }

    internal string? TooltipText { get; private set; }

    internal Point? TooltipAnchorPoint => _tooltipAnchorPoint;

    internal bool IsTooltipSuppressed => _isTooltipSuppressed;

    internal IReadOnlyList<TreemapNodeVisual> NodeVisuals => _nodeVisuals;

    internal TreemapTooltipState? TooltipState => _tooltipState;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arrangedSize = base.ArrangeOverride(finalSize);
        UpdateVisuals(arrangedSize);
        return arrangedSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MetricProperty ||
            change.Property == RootNodeProperty ||
            change.Property == ShowDirectoryNodesProperty ||
            change.Property == LeafGapProperty ||
            change.Property == MinLeafAreaRatioProperty ||
            change.Property == CanvasInsetProperty)
        {
            UpdateVisuals(Bounds.Size);
            InvalidateVisual();
        }

        if (change.Property == SelectedNodeProperty ||
            change.Property == MetricProperty ||
            change.Property == PaletteProperty ||
            change.Property == ShowLabelsProperty ||
            change.Property == ShowMetricValuesProperty ||
            change.Property == VisibleMetricIdsProperty ||
            change.Property == LeafCornerRadiusProperty ||
            change.Property == ShowLeafBordersProperty)
        {
            if ((change.Property == VisibleMetricIdsProperty ||
                 change.Property == MetricProperty) &&
                HoveredNode is not null)
            {
                UpdateTooltipState(HoveredNode);
                RaiseTooltipStateChanged();
            }

            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var drawingBounds = new Rect(Bounds.Size).Deflate(Math.Max(0, CanvasInset));

        context.FillRectangle(CanvasBackgroundBrush, new Rect(Bounds.Size));

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

        var paletteContext = TreemapColorRules.CreatePaletteContext(
            _nodeVisuals
                .Select(static visual => visual.Node)
                .Where(IsLeafNode),
            Metric);

        foreach (var visual in _nodeVisuals)
        {
            var isLeaf = IsLeafNode(visual.Node);
            var interactionState = GetInteractionState(visual.Node);
            var leafFillColor = isLeaf
                ? TreemapColorRules.GetLeafColor(visual.Node, Palette, paletteContext)
                : default;
            if (isLeaf)
            {
                leafFillColor = TreemapInteractionVisuals.GetFillColor(leafFillColor, interactionState);
                DrawLeafNode(context, visual, leafFillColor, interactionState);
            }
            else if (ShowDirectoryNodes && visual.Bounds.Width >= 12 && visual.Bounds.Height >= 12)
            {
                DrawDirectoryNode(context, visual, interactionState);
            }

            if ((isLeaf || ShowDirectoryNodes) &&
                ((ShowLabels && TreemapVisualRules.CanDrawLabel(visual.Node, visual.Bounds)) ||
                 (isLeaf && ShowMetricValues)))
            {
                DrawNodeLabel(context, visual, isLeaf, leafFillColor);
            }
        }

    }

    private void DrawLeafNode(
        DrawingContext context,
        TreemapNodeVisual visual,
        Color fillColor,
        TreemapInteractionState interactionState)
    {
        var fill = new SolidColorBrush(fillColor);
        if (TryCreateLeafRoundedRect(visual.Bounds) is { } roundedRect)
        {
            context.DrawRectangle(fill, pen: null, roundedRect);
            DrawSelectionOverlay(context, visual.Bounds, interactionState, fillColor);
            DrawNodeBorder(context, visual.Node, visual.Bounds, fillColor, interactionState);
            return;
        }

        context.FillRectangle(fill, visual.Bounds);
        DrawSelectionOverlay(context, visual.Bounds, interactionState, fillColor);
        DrawNodeBorder(context, visual.Node, visual.Bounds, fillColor, interactionState);
    }

    private void DrawDirectoryNode(
        DrawingContext context,
        TreemapNodeVisual visual,
        TreemapInteractionState interactionState)
    {
        var directoryFillBrush = DirectoryFillBrush;
        var directoryHeaderBrush = DirectoryHeaderBrush;
        var referenceColor = TryGetSolidColor(DirectoryFillBrush) ?? Colors.Transparent;

        if (TryGetSolidColor(DirectoryFillBrush) is { } fillColor)
        {
            var interactiveFillColor = TreemapInteractionVisuals.GetFillColor(fillColor, interactionState);
            directoryFillBrush = new SolidColorBrush(interactiveFillColor);
            referenceColor = interactiveFillColor;
        }

        if (TryGetSolidColor(DirectoryHeaderBrush) is { } headerColor)
        {
            directoryHeaderBrush = new SolidColorBrush(
                TreemapInteractionVisuals.GetFillColor(headerColor, interactionState));
        }

        context.FillRectangle(directoryFillBrush, visual.Bounds);

        var headerBounds = TreemapVisualRules.GetHeaderBounds(
            visual.Node,
            visual.Bounds,
            includeDirectoryHeader: true);
        if (headerBounds.Height > 0)
        {
            context.FillRectangle(directoryHeaderBrush, headerBounds);
        }

        DrawSelectionOverlay(context, visual.Bounds, interactionState, referenceColor);
        DrawNodeBorder(context, visual.Node, visual.Bounds, referenceColor, interactionState);
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
        var pressedMouseButton = GetPressedMouseButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind);

        if (IsDrillDownGesture(e.Pointer.Type, e.ClickCount, pressedMouseButton, _lastPressedMouseButton))
        {
            RequestDrillDownAt(point);
        }

        _lastPressedMouseButton = pressedMouseButton;
    }

    private void DrawPlaceholder(DrawingContext context, string message)
    {
        var formattedText = new FormattedText(
            message,
            culture: CultureInfo.InvariantCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: 12,
            foreground: PlaceholderForegroundBrush);

        var point = new Point(
            Math.Max(12, (Bounds.Width - formattedText.Width) / 2),
            Math.Max(12, (Bounds.Height - formattedText.Height) / 2));
        context.DrawText(formattedText, point);
    }

    private void DrawNodeLabel(DrawingContext context, TreemapNodeVisual visual, bool isLeaf, Color leafFillColor)
    {
        IBrush labelBrush = isLeaf
            ? GetLeafLabelBrush(leafFillColor)
            : DirectoryLabelBrush;

        if (!isLeaf)
        {
            DrawDirectoryLabel(context, visual, labelBrush);
            return;
        }

        var metricLabel = ShowMetricValues
            ? TryCreateMetricValueLabel(visual.Node, visual.Bounds, labelBrush)
            : null;

        if (ShowLabels && TreemapVisualRules.CanDrawLabel(visual.Node, visual.Bounds))
        {
            DrawLeafNameLabel(context, visual, labelBrush, metricLabel);
        }

        if (metricLabel is not null)
        {
            DrawCenteredMetricLabel(context, visual, metricLabel);
        }
    }

    private static void DrawDirectoryLabel(DrawingContext context, TreemapNodeVisual visual, IBrush labelBrush)
    {
        if (!TreemapVisualRules.CanDrawLabel(visual.Node, visual.Bounds))
        {
            return;
        }

        var labelBounds = TreemapVisualRules.GetLabelBounds(visual.Node, visual.Bounds);
        var nameText = CreateNodeLabelText(
            visual.Node.Name,
            TreemapVisualRules.GetLabelFontSize(visual.Node),
            labelBrush);
        var headerBounds = TreemapVisualRules.GetHeaderBounds(
            visual.Node,
            visual.Bounds,
            includeDirectoryHeader: true);
        var clipBounds = new Rect(labelBounds.X, headerBounds.Y, labelBounds.Width, headerBounds.Height);
        var textOrigin = new Point(labelBounds.X, GetDirectoryLabelOriginY(headerBounds, nameText));

        using var clip = context.PushClip(clipBounds);
        context.DrawText(nameText, textOrigin);
    }

    private static void DrawLeafNameLabel(
        DrawingContext context,
        TreemapNodeVisual visual,
        IBrush labelBrush,
        FormattedText? metricLabel)
    {
        var labelBounds = TreemapVisualRules.GetLabelBounds(visual.Node, visual.Bounds);
        var nameText = CreateNodeLabelText(
            visual.Node.Name,
            TreemapVisualRules.GetLabelFontSize(visual.Node),
            labelBrush);

        if (metricLabel is not null)
        {
            var metricBounds = GetCenteredMetricBounds(visual.Node, visual.Bounds, metricLabel);
            if (!TreemapVisualRules.CanDrawNameAlongsideMetric(visual.Node, visual.Bounds, metricBounds))
            {
                return;
            }
        }

        using var clip = context.PushClip(labelBounds);
        context.DrawText(nameText, new Point(labelBounds.X, labelBounds.Y));
    }

    private static void DrawCenteredMetricLabel(DrawingContext context, TreemapNodeVisual visual, FormattedText metricLabel)
    {
        var metricBounds = GetCenteredMetricBounds(visual.Node, visual.Bounds, metricLabel);
        using var clip = context.PushClip(TreemapVisualRules.GetMetricValueBounds(visual.Node, visual.Bounds));
        context.DrawText(metricLabel, new Point(metricBounds.X, metricBounds.Y));
    }

    private void UpdateVisuals(Size availableSize)
    {
        _layoutSize = availableSize;

        var drawingBounds = new Rect(availableSize).Deflate(Math.Max(0, CanvasInset));
        if (RootNode is null || drawingBounds.Width <= 0 || drawingBounds.Height <= 0)
        {
            _nodeVisuals = [];
            return;
        }

        var visuals = _layout.Calculate(
            RootNode,
            drawingBounds,
            Metric,
            includeDirectoryHeaders: ShowDirectoryNodes,
            minLeafAreaRatio: MinLeafAreaRatio);
        var halfGap = Math.Max(0, LeafGap) / 2d;
        var styledVisuals = new List<TreemapNodeVisual>(visuals.Count);

        foreach (var visual in visuals)
        {
            var bounds = visual.Bounds;
            if (IsLeafNode(visual.Node) && halfGap > 0)
            {
                bounds = TreemapVisualRules.Inset(bounds, halfGap);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    continue;
                }
            }

            styledVisuals.Add(visual with { Bounds = bounds });
        }

        _nodeVisuals = styledVisuals;
    }

    public ProjectNode? HitTestNode(Point point)
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
        if (_isTooltipSuppressed)
        {
            return;
        }

        var hoveredNode = HitTestNode(point);
        if (hoveredNode is null)
        {
            ClearHover();
            return;
        }

        var hoveredNodeChanged = !ReferenceEquals(HoveredNode, hoveredNode);
        HoveredNode = hoveredNode;
        _tooltipAnchorPoint = point;
        if (hoveredNodeChanged || TooltipText is null)
        {
            UpdateTooltipState(hoveredNode);
        }

        RaiseTooltipStateChanged();
        if (hoveredNodeChanged)
        {
            InvalidateVisual();
        }
    }

    public void SetTooltipSuppressed(bool isSuppressed)
    {
        if (_isTooltipSuppressed == isSuppressed)
        {
            return;
        }

        _isTooltipSuppressed = isSuppressed;
        if (isSuppressed)
        {
            ClearHover();
            return;
        }

        RaiseTooltipStateChanged();
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
        _tooltipAnchorPoint = null;
        _tooltipState = null;
        RaiseTooltipStateChanged();
        InvalidateVisual();
    }

    public void SelectNodeAt(Point point)
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

    private void DrawNodeBorder(
        DrawingContext context,
        ProjectNode node,
        Rect bounds,
        Color fillColor,
        TreemapInteractionState interactionState)
    {
        var isLeaf = IsLeafNode(node);
        if (!isLeaf && !ShowDirectoryNodes)
        {
            return;
        }

        if (interactionState == TreemapInteractionState.None)
        {
            DrawBorderRectangle(context, bounds, CreateDefaultBorderPen(node), isLeaf);
            return;
        }

        var accentThickness = TreemapInteractionVisuals.GetAccentThickness(interactionState);
        var accentBrush = interactionState == TreemapInteractionState.Selected
            ? SelectedBorderBrush
            : HoverBorderBrush;
        DrawBorderRectangle(context, bounds, new Pen(accentBrush, accentThickness), isLeaf);

        var innerBounds = TreemapVisualRules.Inset(bounds, Math.Max(accentThickness, 1d));
        if (innerBounds.Width <= 2 || innerBounds.Height <= 2)
        {
            return;
        }

        var contrastBrush = new SolidColorBrush(TreemapInteractionVisuals.GetContrastBorderColor(fillColor));
        DrawBorderRectangle(context, innerBounds, new Pen(contrastBrush, InteractiveContrastThickness), isLeaf);
    }

    private static void DrawSelectionOverlay(
        DrawingContext context,
        Rect bounds,
        TreemapInteractionState interactionState,
        Color fillColor)
    {
        if (!TreemapInteractionVisuals.ShouldDrawStripeOverlay(bounds, interactionState))
        {
            return;
        }

        var stripeBounds = TreemapInteractionVisuals.GetStripeBounds(
            bounds,
            TreemapInteractionVisuals.GetAccentThickness(interactionState));
        if (stripeBounds.Width <= 0 || stripeBounds.Height <= 0)
        {
            return;
        }

        var stripePen = new Pen(
            new SolidColorBrush(TreemapInteractionVisuals.GetStripeColor(fillColor)),
            TreemapInteractionVisuals.GetStripeThickness());
        var stripeSpacing = TreemapInteractionVisuals.GetStripeSpacing();
        var startX = stripeBounds.Left - stripeBounds.Height;
        var endX = stripeBounds.Right + stripeBounds.Height;

        using var clip = context.PushClip(stripeBounds);
        for (var x = startX; x <= endX; x += stripeSpacing)
        {
            context.DrawLine(
                stripePen,
                new Point(x, stripeBounds.Bottom),
                new Point(x + stripeBounds.Height, stripeBounds.Top));
        }
    }

    private Pen? CreateDefaultBorderPen(ProjectNode node)
    {
        var isLeaf = IsLeafNode(node);

        if (!isLeaf && !ShowDirectoryNodes)
        {
            return null;
        }

        if (isLeaf && !ShowLeafBorders)
        {
            return null;
        }

        return isLeaf
            ? new Pen(LeafBorderBrush, 1)
            : new Pen(DirectoryBorderBrush, 1);
    }

    private void DrawBorderRectangle(DrawingContext context, Rect bounds, Pen? pen, bool isLeaf)
    {
        if (pen is null)
        {
            return;
        }

        if (isLeaf && TryCreateLeafRoundedRect(bounds) is { } roundedRect)
        {
            context.DrawRectangle(brush: null, pen, roundedRect);
            return;
        }

        context.DrawRectangle(pen, bounds);
    }

    private TreemapInteractionState GetInteractionState(ProjectNode node)
    {
        if (SelectedNode?.Id == node.Id)
        {
            return TreemapInteractionState.Selected;
        }

        return HoveredNode?.Id == node.Id
            ? TreemapInteractionState.Hovered
            : TreemapInteractionState.None;
    }

    private static Color? TryGetSolidColor(IBrush brush) =>
        brush switch
        {
            ISolidColorBrush solidColorBrush => solidColorBrush.Color,
            _ => null,
        };

    private RoundedRect? TryCreateLeafRoundedRect(Rect bounds)
    {
        if (LeafCornerRadius <= 0)
        {
            return null;
        }

        var radius = Math.Min(LeafCornerRadius, Math.Min(bounds.Width, bounds.Height) / 2d);
        if (radius <= 0)
        {
            return null;
        }

        return new RoundedRect(bounds, new CornerRadius(radius));
    }

    private void UpdateTooltipState(ProjectNode node)
    {
        var relativePath = string.IsNullOrWhiteSpace(node.RelativePath) ? "(root)" : node.RelativePath;
        var items = BuildTooltipItems(node);
        var lines = new List<string> { relativePath };

        foreach (var item in items)
        {
            lines.Add(item switch
            {
                TreemapTooltipValueRow row => $"{row.Label}: {row.Value}",
                TreemapTooltipSeparatorItem => "---",
                _ => string.Empty,
            });
        }

        TooltipText = string.Join(Environment.NewLine, lines);
        _tooltipState = new TreemapTooltipState(
            relativePath,
            new ReadOnlyCollection<TreemapTooltipItem>(items));
    }

    private List<TreemapTooltipItem> BuildTooltipItems(ProjectNode node)
    {
        var items = new List<TreemapTooltipItem>();
        var visibleMetricRows = BuildVisibleMetricRows(node);
        var hiddenMetricRows = BuildHiddenMetricRows(node);
        var infoRows = BuildInfoRows(node);

        AppendTooltipSection(items, visibleMetricRows);
        AppendTooltipSection(items, hiddenMetricRows);
        AppendTooltipSection(items, infoRows);

        return items;
    }

    private List<TreemapTooltipValueRow> BuildVisibleMetricRows(ProjectNode node)
    {
        var rows = new List<TreemapTooltipValueRow>();
        foreach (var metricId in GetNormalizedVisibleMetricIds())
        {
            if (!TryGetMetricLabel(metricId, out var label))
            {
                continue;
            }

            rows.Add(new TreemapTooltipValueRow(label, FormatMetric(node, metricId)));
        }

        var share = RootNode is null
            ? "n/a"
            : FormatShare(GetMetricValue(node), GetMetricValue(RootNode));
        rows.Add(new TreemapTooltipValueRow("Share", share));
        return rows;
    }

    private List<TreemapTooltipValueRow> BuildHiddenMetricRows(ProjectNode node)
    {
        var visibleMetricIds = GetNormalizedVisibleMetricIds().ToHashSet();
        var rows = new List<TreemapTooltipValueRow>();

        foreach (var definition in DefaultMetricCatalog.Instance.GetAll())
        {
            if (visibleMetricIds.Contains(definition.Id) ||
                !node.ComputedMetrics.Values.TryGetValue(definition.Id, out var value) ||
                !value.HasValue)
            {
                continue;
            }

            rows.Add(new TreemapTooltipValueRow(definition.DisplayName, MetricValueFormatter.Format(definition.Id, value, CultureInfo.CurrentCulture)));
        }

        return rows;
    }

    private static List<TreemapTooltipValueRow> BuildInfoRows(ProjectNode node)
    {
        var rows = new List<TreemapTooltipValueRow>
        {
            new("Type", GetKindText(node)),
        };

        if (node.Kind == ProjectNodeKind.File)
        {
            var extension = Path.GetExtension(node.Name) is { Length: > 0 } fileExtension
                ? fileExtension
                : "(none)";
            rows.Add(new TreemapTooltipValueRow("Ext", extension));
            return rows;
        }

        rows.Add(new TreemapTooltipValueRow(
            "Files in subtree",
            node.Summary.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture)));
        return rows;
    }

    private static void AppendTooltipSection(List<TreemapTooltipItem> items, List<TreemapTooltipValueRow> sectionRows)
    {
        if (sectionRows.Count == 0)
        {
            return;
        }

        if (items.Count > 0)
        {
            items.Add(new TreemapTooltipSeparatorItem());
        }

        items.AddRange(sectionRows);
    }

    private void RaiseTooltipStateChanged() => TooltipStateChanged?.Invoke(this, EventArgs.Empty);

    private IReadOnlyList<MetricId> GetNormalizedVisibleMetricIds()
    {
        if (VisibleMetricIds.Count == 0)
        {
            return DefaultMetricCatalog.GetDefaultVisibleMetricIds();
        }

        var normalizedMetricIds = new List<MetricId>(VisibleMetricIds.Count);
        var seenMetricIds = new HashSet<MetricId>();
        foreach (var metricId in VisibleMetricIds)
        {
            var normalizedMetricId = DefaultMetricCatalog.NormalizeMetricId(metricId);
            if (seenMetricIds.Add(normalizedMetricId))
            {
                normalizedMetricIds.Add(normalizedMetricId);
            }
        }

        return normalizedMetricIds.Count == 0
            ? DefaultMetricCatalog.GetDefaultVisibleMetricIds()
            : normalizedMetricIds;
    }

    private static bool TryGetMetricLabel(MetricId metricId, out string label)
    {
        if (DefaultMetricCatalog.Instance.TryGet(metricId, out var definition))
        {
            label = definition.DisplayName;
            return true;
        }

        label = string.Empty;
        return false;
    }

    private static List<FormattedText> WrapTooltipText(
        string text,
        double maxWidth,
        double fontSize,
        IBrush foreground,
        bool preferPathBreaks)
    {
        var lines = new List<FormattedText>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        if (!preferPathBreaks)
        {
            lines.Add(CreateTooltipText(text, fontSize, foreground));
            return lines;
        }

        var tokens = TokenizePath(text);
        var current = string.Empty;

        foreach (var token in tokens)
        {
            var candidate = string.Concat(current, token);
            if (current.Length == 0 || CreateTooltipText(candidate, fontSize, foreground).Width <= maxWidth)
            {
                current = candidate;
                continue;
            }

            AppendWrappedToken(lines, current, maxWidth, fontSize, foreground);
            current = token;
        }

        if (current.Length > 0)
        {
            AppendWrappedToken(lines, current, maxWidth, fontSize, foreground);
        }

        return lines;
    }

    private static void AppendWrappedToken(
        List<FormattedText> lines,
        string text,
        double maxWidth,
        double fontSize,
        IBrush foreground)
    {
        var remaining = text;
        while (remaining.Length > 0)
        {
            var splitLength = remaining.Length;
            while (splitLength > 1 &&
                   CreateTooltipText(remaining[..splitLength], fontSize, foreground).Width > maxWidth)
            {
                splitLength--;
            }

            lines.Add(CreateTooltipText(remaining[..splitLength], fontSize, foreground));
            remaining = remaining[splitLength..];
        }
    }

    private static List<string> TokenizePath(string text)
    {
        var tokens = new List<string>();
        var segmentStart = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '/' && text[index] != '\\')
            {
                continue;
            }

            tokens.Add(text[segmentStart..(index + 1)]);
            segmentStart = index + 1;
        }

        if (segmentStart < text.Length)
        {
            tokens.Add(text[segmentStart..]);
        }

        return tokens;
    }

    private static FormattedText CreateTooltipText(string text, double fontSize, IBrush foreground)
    {
        return new FormattedText(
            text,
            culture: CultureInfo.CurrentCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: fontSize,
            foreground: foreground);
    }

    private static FormattedText CreateNodeLabelText(string text, double fontSize, IBrush foreground)
    {
        return new FormattedText(
            text,
            culture: CultureInfo.CurrentCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: fontSize,
            foreground: foreground);
    }

    private static double GetDirectoryLabelOriginY(Rect headerBounds, FormattedText formattedText)
    {
        var inkTopOffset = Math.Max(0, formattedText.Height + formattedText.OverhangAfter - formattedText.Extent);
        var inkPadding = Math.Max(0, (headerBounds.Height - formattedText.Extent) / 2);
        return headerBounds.Y + inkPadding - inkTopOffset;
    }

    private IBrush GetLeafLabelBrush(Color fillColor) =>
        TreemapColorRules.ShouldUseDarkLeafLabel(fillColor)
            ? LeafDarkLabelBrush
            : LeafLightLabelBrush;

    internal string? GetMetricValueLabel(ProjectNode node, Rect bounds)
    {
        if (!ShowMetricValues ||
            node.Kind != ProjectNodeKind.File ||
            !TreemapVisualRules.CanDrawLabel(node, bounds) ||
            !TreemapVisualRules.CanDrawMetricValueLabel(node, bounds))
        {
            return null;
        }

        var metricValue = TryGetMetricValueLabelValue(node);
        return metricValue is null
            ? null
            : MetricValueFormatter.FormatCompact(Metric, metricValue.Value, CultureInfo.CurrentCulture);
    }

    private FormattedText? TryCreateMetricValueLabel(ProjectNode node, Rect bounds, IBrush foreground)
    {
        var text = GetMetricValueLabel(node, bounds);
        if (text is null)
        {
            return null;
        }

        var metricBounds = TreemapVisualRules.GetMetricValueBounds(node, bounds);

        var minFontSize = TreemapVisualRules.GetMetricLabelMinFontSize();
        for (var fontSize = TreemapVisualRules.GetMetricLabelMaxFontSize(bounds); fontSize >= minFontSize; fontSize -= 0.5d)
        {
            var metricText = CreateNodeLabelText(text, fontSize, foreground);
            if (metricText.Width <= metricBounds.Width && metricText.Height <= metricBounds.Height)
            {
                return metricText;
            }
        }

        return null;
    }

    private MetricValue? TryGetMetricValueLabelValue(ProjectNode node)
    {
        var metricValue = node.ComputedMetrics.GetOrDefault(DefaultMetricCatalog.NormalizeMetricId(Metric));
        return metricValue.HasValue
            ? metricValue
            : null;
    }

    private static Rect GetCenteredMetricBounds(ProjectNode node, Rect bounds, FormattedText metricLabel)
    {
        var metricBounds = TreemapVisualRules.GetMetricValueBounds(node, bounds);
        var x = metricBounds.X + Math.Max(0d, (metricBounds.Width - metricLabel.Width) / 2d);
        var y = metricBounds.Y + Math.Max(0d, (metricBounds.Height - metricLabel.Height) / 2d);
        return new Rect(x, y, metricLabel.Width, metricLabel.Height);
    }

    private double GetMetricValue(ProjectNode node) =>
        node.ComputedMetrics.TryGetNumber(DefaultMetricCatalog.NormalizeMetricId(Metric)) ?? 0;

    private static string FormatShare(double current, double total) =>
        total <= 0 || current <= 0 ? "n/a" : $"{current / total:P1}";

    private static string GetKindText(ProjectNode node) =>
        node.Kind switch
        {
            ProjectNodeKind.Root => "Root",
            ProjectNodeKind.Directory => "Directory",
            _ => "File",
        };

    private static string FormatMetric(ProjectNode node, MetricId metricId) =>
        MetricValueFormatter.Format(metricId, node.ComputedMetrics.GetOrDefault(metricId), CultureInfo.CurrentCulture);

    private static bool IsLeafNode(ProjectNode node) =>
        node.Kind == ProjectNodeKind.File || node.Children.Count == 0;

    internal static bool IsDrillDownGesture(
        PointerType pointerType,
        int clickCount,
        MouseButton? currentMouseButton,
        MouseButton? previousMouseButton)
    {
        if (clickCount != 2)
        {
            return false;
        }

        return pointerType != PointerType.Mouse ||
               currentMouseButton is not null && currentMouseButton == previousMouseButton;
    }

    internal static MouseButton? GetPressedMouseButton(PointerUpdateKind pointerUpdateKind) =>
        pointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
            PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            PointerUpdateKind.XButton1Pressed => MouseButton.XButton1,
            PointerUpdateKind.XButton2Pressed => MouseButton.XButton2,
            _ => null,
        };

    private static bool CanDrillDown(ProjectNode? node) =>
        node is not null &&
        node.Kind != ProjectNodeKind.File &&
        node.Children.Count > 0;

}

internal abstract record TreemapTooltipItem;

internal sealed record TreemapTooltipValueRow(string Label, string Value) : TreemapTooltipItem;

internal sealed record TreemapTooltipSeparatorItem : TreemapTooltipItem;

internal sealed record TreemapTooltipState(
    string PathText,
    IReadOnlyList<TreemapTooltipItem> Items);
