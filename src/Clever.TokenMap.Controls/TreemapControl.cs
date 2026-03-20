using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Clever.TokenMap.Controls.Layout;
using Clever.TokenMap.Controls.Models;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Controls;

public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<string> MetricProperty =
        AvaloniaProperty.Register<TreemapControl, string>(nameof(Metric), "Tokens");

    public static readonly StyledProperty<ProjectNode?> RootNodeProperty =
        AvaloniaProperty.Register<TreemapControl, ProjectNode?>(nameof(RootNode));

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

    internal ProjectNode? HoveredNode { get; private set; }

    internal ProjectNode? PressedNode { get; private set; }

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
            var fill = new SolidColorBrush(CreateColor(visual.Node.Id, visual.Depth));
            context.FillRectangle(fill, visual.Bounds);
            context.DrawRectangle(
                pen: new Pen(new SolidColorBrush(Color.Parse("#121A25")), 1),
                rect: visual.Bounds);

            if (visual.Bounds.Width >= 56 && visual.Bounds.Height >= 22)
            {
                var formattedText = new FormattedText(
                    visual.Node.Name,
                    culture: System.Globalization.CultureInfo.InvariantCulture,
                    flowDirection: FlowDirection.LeftToRight,
                    typeface: Typeface.Default,
                    emSize: 12,
                    foreground: Brushes.White);
                context.DrawText(formattedText, new Point(visual.Bounds.X + 4, visual.Bounds.Y + 4));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HoveredNode = HitTestNode(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HoveredNode = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PressedNode = HitTestNode(e.GetPosition(this));
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

    private static Color CreateColor(string seed, int depth)
    {
        var hash = seed.Aggregate(17, (current, character) => current * 31 + character);
        var hue = Math.Abs(hash % 360);
        var saturation = Math.Clamp(0.55 + depth * 0.05, 0.55, 0.8);
        var value = Math.Clamp(0.70 - depth * 0.05, 0.40, 0.75);

        return ColorFromHsv(hue, saturation, value);
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var section = hue / 60d;
        var x = chroma * (1 - Math.Abs(section % 2 - 1));
        var m = value - chroma;

        (double r, double g, double b) = section switch
        {
            >= 0 and < 1 => (chroma, x, 0d),
            >= 1 and < 2 => (x, chroma, 0d),
            >= 2 and < 3 => (0d, chroma, x),
            >= 3 and < 4 => (0d, x, chroma),
            >= 4 and < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
