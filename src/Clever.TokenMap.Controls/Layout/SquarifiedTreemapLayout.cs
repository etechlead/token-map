using Avalonia;
using Clever.TokenMap.Controls.Models;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Controls.Layout;

public sealed class SquarifiedTreemapLayout
{
    public IReadOnlyList<TreemapNodeVisual> Calculate(ProjectNode rootNode, Rect bounds, string metric)
    {
        ArgumentNullException.ThrowIfNull(rootNode);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return [];
        }

        var visuals = new List<TreemapNodeVisual>();
        LayoutNode(rootNode, bounds, metric, visuals, depth: 0);
        return visuals;
    }

    private void LayoutNode(
        ProjectNode node,
        Rect bounds,
        string metric,
        List<TreemapNodeVisual> visuals,
        int depth)
    {
        var items = node.Children
            .Select(child => new WeightedNode(child, GetWeight(child, metric)))
            .Where(item => item.Weight > 0)
            .OrderByDescending(item => item.Weight)
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        var remainingBounds = bounds;
        var remainingWeight = items.Sum(item => item.Weight);
        var row = new List<WeightedNode>();

        while (items.Count > 0)
        {
            var candidate = items[0];
            if (row.Count == 0 || ImprovesAspectRatio(row, candidate, remainingBounds))
            {
                row.Add(candidate);
                items.RemoveAt(0);
                continue;
            }

            remainingBounds = LayoutRow(row, remainingBounds, remainingWeight, visuals, metric, depth);
            remainingWeight -= row.Sum(item => item.Weight);
            row.Clear();
        }

        if (row.Count > 0)
        {
            LayoutRow(row, remainingBounds, remainingWeight, visuals, metric, depth);
        }
    }

    private Rect LayoutRow(
        IReadOnlyList<WeightedNode> row,
        Rect bounds,
        double totalWeight,
        List<TreemapNodeVisual> visuals,
        string metric,
        int depth)
    {
        var rowWeight = row.Sum(item => item.Weight);
        if (rowWeight <= 0 || totalWeight <= 0)
        {
            return bounds;
        }

        var isHorizontal = bounds.Width >= bounds.Height;
        var boundsArea = bounds.Width * bounds.Height;
        var rowArea = boundsArea * (rowWeight / totalWeight);

        if (isHorizontal)
        {
            var rowHeight = rowArea / bounds.Width;
            var x = bounds.X;

            foreach (var item in row)
            {
                var itemWidth = rowHeight <= 0
                    ? 0
                    : rowArea * (item.Weight / rowWeight) / rowHeight;
                var itemBounds = new Rect(x, bounds.Y, itemWidth, rowHeight);
                visuals.Add(new TreemapNodeVisual(item.Node, itemBounds, depth));

                if (item.Node.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
                {
                    LayoutNode(item.Node, Inset(itemBounds, 1), metric, visuals, depth + 1);
                }

                x += itemWidth;
            }

            return new Rect(bounds.X, bounds.Y + rowHeight, bounds.Width, Math.Max(0, bounds.Height - rowHeight));
        }

        var rowWidth = rowArea / bounds.Height;
        var y = bounds.Y;

        foreach (var item in row)
        {
            var itemHeight = rowWidth <= 0
                ? 0
                : rowArea * (item.Weight / rowWeight) / rowWidth;
            var itemBounds = new Rect(bounds.X, y, rowWidth, itemHeight);
            visuals.Add(new TreemapNodeVisual(item.Node, itemBounds, depth));

            if (item.Node.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
            {
                LayoutNode(item.Node, Inset(itemBounds, 1), metric, visuals, depth + 1);
            }

            y += itemHeight;
        }

        return new Rect(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.Width - rowWidth), bounds.Height);
    }

    private static Rect Inset(Rect rect, double inset) =>
        rect.Width <= inset * 2 || rect.Height <= inset * 2
            ? rect
            : new Rect(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);

    private static bool ImprovesAspectRatio(IReadOnlyList<WeightedNode> row, WeightedNode candidate, Rect bounds)
    {
        var side = Math.Max(1d, Math.Min(bounds.Width, bounds.Height));
        var currentWorst = GetWorstAspectRatio(row, side);

        var extendedRow = new List<WeightedNode>(row.Count + 1);
        extendedRow.AddRange(row);
        extendedRow.Add(candidate);

        var nextWorst = GetWorstAspectRatio(extendedRow, side);
        return nextWorst <= currentWorst;
    }

    private static double GetWorstAspectRatio(IReadOnlyList<WeightedNode> row, double side)
    {
        if (row.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var sum = row.Sum(item => item.Weight);
        var max = row.Max(item => item.Weight);
        var min = row.Min(item => item.Weight);

        if (sum <= 0 || min <= 0)
        {
            return double.PositiveInfinity;
        }

        var sideSquared = side * side;
        var sumSquared = sum * sum;

        return Math.Max(
            sideSquared * max / sumSquared,
            sumSquared / (sideSquared * min));
    }

    private static double GetWeight(ProjectNode node, string metric) =>
        metric switch
        {
            "Total lines" => node.Metrics.TotalLines,
            "Code lines" => node.Metrics.CodeLines ?? 0,
            _ => node.Metrics.Tokens,
        };

    private sealed record WeightedNode(ProjectNode Node, double Weight);
}
