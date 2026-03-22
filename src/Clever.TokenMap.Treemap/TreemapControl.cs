using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using System.IO;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapControl : Control
{
    private const double TooltipMargin = 12;
    private const double TooltipPaddingX = 14;
    private const double TooltipPaddingY = 12;
    private const double TooltipHeaderSpacing = 10;
    private const double TooltipRowSpacing = 6;
    private const double TooltipColumnSpacing = 12;
    private const double TooltipMaxWidth = 360;

    public static readonly StyledProperty<AnalysisMetric> MetricProperty =
        AvaloniaProperty.Register<TreemapControl, AnalysisMetric>(nameof(Metric), AnalysisMetric.Tokens);

    public static readonly StyledProperty<ProjectNode?> RootNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(RootNode));

    public static readonly StyledProperty<ProjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(SelectedNode), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private readonly SquarifiedTreemapLayout _layout = new();
    private IReadOnlyList<TreemapNodeVisual> _nodeVisuals = [];
    private Size _layoutSize;
    private Point? _tooltipAnchorPoint;
    private static readonly IBrush SelectedAccentFallbackBrush = new SolidColorBrush(Color.Parse("#E7F2FF"));
    private static readonly IBrush HoverAccentFallbackBrush = new SolidColorBrush(Color.Parse("#8BC3FF"));
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

    internal Point? TooltipAnchorPoint => _tooltipAnchorPoint;

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

        DrawTooltipOverlay(context);
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
            TooltipText = BuildTooltip(hoveredNode);
        }
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

    private void DrawTooltipOverlay(DrawingContext context)
    {
        if (_tooltipAnchorPoint is null || HoveredNode is null || string.IsNullOrWhiteSpace(TooltipText))
        {
            return;
        }

        var isDarkTheme = IsDarkTheme();
        var pathText = string.IsNullOrWhiteSpace(HoveredNode.RelativePath) ? "(root)" : HoveredNode.RelativePath;
        var rows = BuildTooltipRows(HoveredNode);
        var labelBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#AAB4C0"))
            : new SolidColorBrush(Color.Parse("#667085"));
        var valueBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#F3F4F6"))
            : new SolidColorBrush(Color.Parse("#1F2933"));
        var backgroundBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#1C2128"))
            : new SolidColorBrush(Color.Parse("#FFFFFF"));
        var borderBrush = isDarkTheme
            ? new SolidColorBrush(Color.Parse("#3A4350"))
            : new SolidColorBrush(Color.Parse("#D7DCE2"));

        var labelTexts = new List<FormattedText>(rows.Length);
        var valueTexts = new List<FormattedText>(rows.Length);
        var labelColumnWidth = 0d;
        var valueColumnWidth = 0d;
        var rowsHeight = 0d;

        foreach (var row in rows)
        {
            var labelText = new FormattedText(
                row.Label,
                culture: CultureInfo.CurrentCulture,
                flowDirection: FlowDirection.LeftToRight,
                typeface: Typeface.Default,
                emSize: 11,
                foreground: labelBrush);
            var valueText = new FormattedText(
                row.Value,
                culture: CultureInfo.CurrentCulture,
                flowDirection: FlowDirection.LeftToRight,
                typeface: Typeface.Default,
                emSize: 12,
                foreground: valueBrush);

            labelTexts.Add(labelText);
            valueTexts.Add(valueText);
            labelColumnWidth = Math.Max(labelColumnWidth, labelText.Width);
            valueColumnWidth = Math.Max(valueColumnWidth, valueText.Width);
            rowsHeight += Math.Max(labelText.Height, valueText.Height);
        }

        if (rows.Length > 1)
        {
            rowsHeight += TooltipRowSpacing * (rows.Length - 1);
        }

        var tooltipContentMaxWidth = TooltipMaxWidth - (TooltipPaddingX * 2);
        var minimumContentWidth = Math.Min(
            tooltipContentMaxWidth,
            Math.Max(220, labelColumnWidth + TooltipColumnSpacing + valueColumnWidth));
        var wrappedPathLines = WrapTooltipText(pathText, minimumContentWidth, 13, valueBrush, true);
        var wrappedPathWidth = 0d;
        var wrappedPathHeight = 0d;

        foreach (var line in wrappedPathLines)
        {
            wrappedPathWidth = Math.Max(wrappedPathWidth, line.Width);
            wrappedPathHeight += line.Height;
        }

        if (wrappedPathLines.Count > 1)
        {
            wrappedPathHeight += TooltipRowSpacing * (wrappedPathLines.Count - 1);
        }

        var contentWidth = Math.Min(
            tooltipContentMaxWidth,
            Math.Max(wrappedPathWidth, labelColumnWidth + TooltipColumnSpacing + valueColumnWidth));
        var tooltipWidth = contentWidth + (TooltipPaddingX * 2);
        var tooltipHeight = wrappedPathHeight + TooltipHeaderSpacing + rowsHeight + (TooltipPaddingY * 2);
        var tooltipX = _tooltipAnchorPoint.Value.X + TooltipMargin;
        var tooltipY = _tooltipAnchorPoint.Value.Y + TooltipMargin;

        if (tooltipX + tooltipWidth > Bounds.Width - 6)
        {
            tooltipX = _tooltipAnchorPoint.Value.X - tooltipWidth - TooltipMargin;
        }

        if (tooltipY + tooltipHeight > Bounds.Height - 6)
        {
            tooltipY = _tooltipAnchorPoint.Value.Y - tooltipHeight - TooltipMargin;
        }

        tooltipX = Math.Clamp(tooltipX, 6, Math.Max(6, Bounds.Width - tooltipWidth - 6));
        tooltipY = Math.Clamp(tooltipY, 6, Math.Max(6, Bounds.Height - tooltipHeight - 6));

        var tooltipBounds = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        var tooltipPen = new Pen(borderBrush, 1);
        context.DrawRectangle(backgroundBrush, tooltipPen, tooltipBounds);

        var contentBounds = tooltipBounds.Deflate(new Thickness(TooltipPaddingX, TooltipPaddingY));
        var textY = contentBounds.Y;
        using var clip = context.PushClip(contentBounds);

        foreach (var pathLine in wrappedPathLines)
        {
            context.DrawText(pathLine, new Point(contentBounds.X, textY));
            textY += pathLine.Height + TooltipRowSpacing;
        }

        textY += TooltipHeaderSpacing - TooltipRowSpacing;

        for (var index = 0; index < rows.Length; index++)
        {
            var rowHeight = Math.Max(labelTexts[index].Height, valueTexts[index].Height);
            var valueX = contentBounds.X + labelColumnWidth + TooltipColumnSpacing;
            context.DrawText(labelTexts[index], new Point(contentBounds.X, textY));
            context.DrawText(valueTexts[index], new Point(valueX, textY));
            textY += rowHeight + TooltipRowSpacing;
        }
    }

    private (string Label, string Value)[] BuildTooltipRows(ProjectNode node)
    {
        var share = RootNode is null
            ? "n/a"
            : FormatShare(GetMetricValue(node), GetMetricValue(RootNode));
        var breakdown = $"{node.Metrics.NonEmptyLines:N0}/{node.Metrics.BlankLines:N0}";
        var extension = node.Kind == Core.Enums.ProjectNodeKind.File
            ? Path.GetExtension(node.Name) is { Length: > 0 } fileExtension ? fileExtension : "(none)"
            : "n/a";

        return
        [
            ("Type", GetKindText(node)),
            ("Tokens", node.Metrics.Tokens.ToString("N0", CultureInfo.CurrentCulture)),
            ("Share", share),
            ("Lines", node.Metrics.TotalLines.ToString("N0", CultureInfo.CurrentCulture)),
            ("Non-empty/Blank", breakdown),
            ("Ext", extension),
            ("Files in subtree", node.Metrics.DescendantFileCount.ToString("N0", CultureInfo.CurrentCulture)),
        ];
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

    private bool IsDarkTheme()
    {
        var variant = Application.Current?.ActualThemeVariant ?? ActualThemeVariant;
        return variant == ThemeVariant.Dark;
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
