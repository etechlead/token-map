using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapTooltipOverlay : Control
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

    public static readonly StyledProperty<TreemapControl?> TargetProperty =
        AvaloniaProperty.Register<TreemapTooltipOverlay, TreemapControl?>(nameof(Target));

    static TreemapTooltipOverlay()
    {
        AffectsRender<TreemapTooltipOverlay>(TargetProperty, BoundsProperty);
    }

    public TreemapControl? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != TargetProperty)
        {
            return;
        }

        if (change.OldValue is TreemapControl oldTarget)
        {
            oldTarget.TooltipStateChanged -= TargetOnTooltipStateChanged;
            oldTarget.PropertyChanged -= TargetOnPropertyChanged;
        }

        if (change.NewValue is TreemapControl newTarget)
        {
            newTarget.TooltipStateChanged += TargetOnTooltipStateChanged;
            newTarget.PropertyChanged += TargetOnPropertyChanged;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Target is not { TooltipState: { } state, TooltipAnchorPoint: { } anchorPoint } target ||
            target.IsTooltipSuppressed)
        {
            return;
        }

        var renderedItems = new List<TooltipRenderedItem>(state.Items.Count);
        var labelColumnWidth = 0d;
        var valueColumnWidth = 0d;
        var rowsHeight = 0d;

        for (var index = 0; index < state.Items.Count; index++)
        {
            var item = state.Items[index];
            if (item is TreemapTooltipValueRow row)
            {
                var labelText = CreateText(row.Label, 11, target.TooltipLabelBrush);
                var valueText = CreateText(row.Value, 12, target.TooltipValueBrush);
                renderedItems.Add(new TooltipRenderedRow(labelText, valueText));
                labelColumnWidth = Math.Max(labelColumnWidth, labelText.Width);
                valueColumnWidth = Math.Max(valueColumnWidth, valueText.Width);
                rowsHeight += Math.Max(labelText.Height, valueText.Height);
            }
            else
            {
                renderedItems.Add(new TooltipRenderedSeparator());
                rowsHeight += (TooltipSeparatorSpacing * 2) + TooltipSeparatorThickness;
            }

            if (index < state.Items.Count - 1 &&
                item is TreemapTooltipValueRow &&
                state.Items[index + 1] is TreemapTooltipValueRow)
            {
                rowsHeight += TooltipRowSpacing;
            }
        }

        var tooltipContentMaxWidth = TooltipMaxWidth - (TooltipPaddingX * 2);
        var minimumContentWidth = Math.Min(
            tooltipContentMaxWidth,
            Math.Max(220, labelColumnWidth + TooltipColumnSpacing + valueColumnWidth));
        var wrappedPathLines = WrapTooltipText(state.PathText, minimumContentWidth, 13, target.TooltipValueBrush);
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
        var tooltipX = anchorPoint.X + TooltipMargin;
        var tooltipY = anchorPoint.Y + TooltipMargin;

        if (tooltipX + tooltipWidth > target.Bounds.Width - 6)
        {
            tooltipX = anchorPoint.X - tooltipWidth - TooltipMargin;
        }

        if (tooltipY + tooltipHeight > target.Bounds.Height - 6)
        {
            tooltipY = anchorPoint.Y - tooltipHeight - TooltipMargin;
        }

        tooltipX = Math.Clamp(tooltipX, 6, Math.Max(6, target.Bounds.Width - tooltipWidth - 6));
        tooltipY = Math.Clamp(tooltipY, 6, Math.Max(6, target.Bounds.Height - tooltipHeight - 6));

        var tooltipBounds = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        context.DrawRectangle(target.TooltipBackgroundBrush, new Pen(target.TooltipBorderBrush, 1), tooltipBounds);

        var contentBounds = tooltipBounds.Deflate(new Thickness(TooltipPaddingX, TooltipPaddingY));
        var textY = contentBounds.Y;
        using var clip = context.PushClip(contentBounds);

        foreach (var pathLine in wrappedPathLines)
        {
            context.DrawText(pathLine, new Point(contentBounds.X, textY));
            textY += pathLine.Height + TooltipRowSpacing;
        }

        textY += TooltipHeaderSpacing - TooltipRowSpacing;

        for (var index = 0; index < renderedItems.Count; index++)
        {
            switch (renderedItems[index])
            {
                case TooltipRenderedRow row:
                {
                    var rowHeight = Math.Max(row.Label.Height, row.Value.Height);
                    var valueX = contentBounds.X + labelColumnWidth + TooltipColumnSpacing;
                    context.DrawText(row.Label, new Point(contentBounds.X, textY));
                    context.DrawText(row.Value, new Point(valueX, textY));
                    textY += rowHeight;
                    break;
                }
                case TooltipRenderedSeparator:
                {
                    textY += TooltipSeparatorSpacing;
                    var separatorY = textY + (TooltipSeparatorThickness / 2d);
                    context.DrawLine(
                        new Pen(target.TooltipBorderBrush, TooltipSeparatorThickness),
                        new Point(contentBounds.X, separatorY),
                        new Point(contentBounds.Right, separatorY));
                    textY += TooltipSeparatorThickness + TooltipSeparatorSpacing;
                    break;
                }
            }

            if (index < renderedItems.Count - 1 &&
                renderedItems[index] is TooltipRenderedRow &&
                renderedItems[index + 1] is TooltipRenderedRow)
            {
                textY += TooltipRowSpacing;
            }
        }
    }

    private void TargetOnTooltipStateChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void TargetOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TreemapControl.TooltipLabelBrushProperty ||
            e.Property == TreemapControl.TooltipValueBrushProperty ||
            e.Property == TreemapControl.TooltipBackgroundBrushProperty ||
            e.Property == TreemapControl.TooltipBorderBrushProperty ||
            e.Property == TreemapControl.BoundsProperty)
        {
            InvalidateVisual();
        }
    }

    private static FormattedText CreateText(string text, double fontSize, IBrush foreground) =>
        new(
            text,
            culture: CultureInfo.CurrentCulture,
            flowDirection: FlowDirection.LeftToRight,
            typeface: Typeface.Default,
            emSize: fontSize,
            foreground: foreground);

    private static List<FormattedText> WrapTooltipText(string text, double maxWidth, double fontSize, IBrush foreground)
    {
        var lines = new List<FormattedText>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        var tokens = TokenizePath(text);
        var current = string.Empty;

        foreach (var token in tokens)
        {
            var candidate = string.Concat(current, token);
            if (current.Length == 0 || CreateText(candidate, fontSize, foreground).Width <= maxWidth)
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
            while (splitLength > 1 && CreateText(remaining[..splitLength], fontSize, foreground).Width > maxWidth)
            {
                splitLength--;
            }

            lines.Add(CreateText(remaining[..splitLength], fontSize, foreground));
            remaining = remaining[splitLength..];
        }
    }

    private static List<string> TokenizePath(string text)
    {
        var tokens = new List<string>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('/' or '\\'))
            {
                continue;
            }

            tokens.Add(text[start..(index + 1)]);
            start = index + 1;
        }

        if (start < text.Length)
        {
            tokens.Add(text[start..]);
        }

        return tokens;
    }

    private abstract record TooltipRenderedItem;

    private sealed record TooltipRenderedRow(FormattedText Label, FormattedText Value) : TooltipRenderedItem;

    private sealed record TooltipRenderedSeparator : TooltipRenderedItem;
}
