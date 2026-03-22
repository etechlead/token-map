using Avalonia;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

public sealed class SquarifiedTreemapLayout
{
    private const int BalancedSplitMinChildCount = 3;
    private const double BalancedSplitMaxShareThreshold = 0.45d;
    private const double BalancedSplitSkewRatioThreshold = 6d;
    private const double MinChildLayoutShortSide = 12d;

    public IReadOnlyList<TreemapNodeVisual> Calculate(ProjectNode rootNode, Rect bounds, AnalysisMetric metric)
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
        AnalysisMetric metric,
        List<TreemapNodeVisual> visuals,
        int depth)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var items = node.Children
            .Select(child => new WeightedNode(child, GetWeight(child, metric)))
            .Where(item => item.Weight > 0)
            .OrderByDescending(item => item.Weight)
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        var boundsArea = bounds.Width * bounds.Height;
        var totalWeight = items.Sum(item => item.Weight);
        if (boundsArea <= 0 || totalWeight <= 0)
        {
            return;
        }

        items = items
            .Select(item => item with { Area = boundsArea * (item.Weight / totalWeight) })
            .ToList();

        // Heavily skewed sibling sets tend to collapse into stripe-like tails with greedy squarify.
        if (ShouldUseBalancedSplit(items, totalWeight))
        {
            LayoutBalanced(items, 0, items.Count, totalWeight, bounds, visuals, metric, depth);
            return;
        }

        var remainingBounds = bounds;
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

            remainingBounds = LayoutRow(row, remainingBounds, visuals, metric, depth);
            row.Clear();
        }

        if (row.Count > 0)
        {
            LayoutRow(row, remainingBounds, visuals, metric, depth);
        }
    }

    private Rect LayoutRow(
        IReadOnlyList<WeightedNode> row,
        Rect bounds,
        List<TreemapNodeVisual> visuals,
        AnalysisMetric metric,
        int depth)
    {
        var rowArea = row.Sum(item => item.Area);
        if (rowArea <= 0)
        {
            return bounds;
        }

        var isHorizontal = bounds.Width >= bounds.Height;

        if (isHorizontal)
        {
            var rowWidth = rowArea / bounds.Height;
            var y = bounds.Y;

            foreach (var item in row)
            {
                var itemHeight = rowWidth <= 0
                    ? 0
                    : item.Area / rowWidth;
                var itemBounds = new Rect(bounds.X, y, rowWidth, itemHeight);
                AddVisual(item.Node, itemBounds, metric, visuals, depth);
                y += itemHeight;
            }

            return new Rect(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.Width - rowWidth), bounds.Height);
        }

        var rowHeight = rowArea / bounds.Width;
        var x = bounds.X;

        foreach (var item in row)
        {
            var itemWidth = rowHeight <= 0
                ? 0
                : item.Area / rowHeight;
            var itemBounds = new Rect(x, bounds.Y, itemWidth, rowHeight);
            AddVisual(item.Node, itemBounds, metric, visuals, depth);
            x += itemWidth;
        }

        return new Rect(bounds.X, bounds.Y + rowHeight, bounds.Width, Math.Max(0, bounds.Height - rowHeight));
    }

    private void LayoutBalanced(
        IReadOnlyList<WeightedNode> items,
        int startIndex,
        int endIndex,
        double totalWeight,
        Rect bounds,
        List<TreemapNodeVisual> visuals,
        AnalysisMetric metric,
        int depth)
    {
        if (startIndex >= endIndex || bounds.Width <= 0 || bounds.Height <= 0 || totalWeight <= 0)
        {
            return;
        }

        if (endIndex - startIndex == 1)
        {
            AddVisual(items[startIndex].Node, bounds, metric, visuals, depth);
            return;
        }

        var splitIndex = FindBalancedSplitIndex(items, startIndex, endIndex, totalWeight, out var leadingWeight);
        if (splitIndex <= startIndex || splitIndex >= endIndex || leadingWeight <= 0 || leadingWeight >= totalWeight)
        {
            AddVisual(items[startIndex].Node, bounds, metric, visuals, depth);
            LayoutBalanced(
                items,
                startIndex + 1,
                endIndex,
                totalWeight - items[startIndex].Weight,
                bounds,
                visuals,
                metric,
                depth);
            return;
        }

        if (bounds.Width >= bounds.Height)
        {
            var leadingWidth = bounds.Width * (leadingWeight / totalWeight);
            var leadingBounds = new Rect(bounds.X, bounds.Y, leadingWidth, bounds.Height);
            var trailingBounds = new Rect(
                bounds.X + leadingWidth,
                bounds.Y,
                Math.Max(0, bounds.Width - leadingWidth),
                bounds.Height);

            LayoutBalanced(items, startIndex, splitIndex, leadingWeight, leadingBounds, visuals, metric, depth);
            LayoutBalanced(items, splitIndex, endIndex, totalWeight - leadingWeight, trailingBounds, visuals, metric, depth);
            return;
        }

        var leadingHeight = bounds.Height * (leadingWeight / totalWeight);
        var topBounds = new Rect(bounds.X, bounds.Y, bounds.Width, leadingHeight);
        var bottomBounds = new Rect(
            bounds.X,
            bounds.Y + leadingHeight,
            bounds.Width,
            Math.Max(0, bounds.Height - leadingHeight));

        LayoutBalanced(items, startIndex, splitIndex, leadingWeight, topBounds, visuals, metric, depth);
        LayoutBalanced(items, splitIndex, endIndex, totalWeight - leadingWeight, bottomBounds, visuals, metric, depth);
    }

    private void AddVisual(
        ProjectNode node,
        Rect bounds,
        AnalysisMetric metric,
        List<TreemapNodeVisual> visuals,
        int depth)
    {
        visuals.Add(new TreemapNodeVisual(node, bounds, depth));

        if (node.Kind is ProjectNodeKind.Directory or ProjectNodeKind.Root)
        {
            var childBounds = TreemapVisualRules.GetContentBounds(node, bounds);
            if (CanLayoutChildren(childBounds))
            {
                LayoutNode(node, childBounds, metric, visuals, depth + 1);
            }
        }
    }

    private static bool CanLayoutChildren(Rect childBounds) =>
        childBounds.Width >= MinChildLayoutShortSide &&
        childBounds.Height >= MinChildLayoutShortSide;

    private static bool ShouldUseBalancedSplit(IReadOnlyList<WeightedNode> items, double totalWeight)
    {
        if (items.Count < BalancedSplitMinChildCount || totalWeight <= 0)
        {
            return false;
        }

        var maxWeight = items[0].Weight;
        var minWeight = items[^1].Weight;
        if (minWeight <= 0)
        {
            return true;
        }

        var largestShare = maxWeight / totalWeight;
        var skewRatio = maxWeight / minWeight;
        return largestShare >= BalancedSplitMaxShareThreshold ||
               skewRatio >= BalancedSplitSkewRatioThreshold;
    }

    private static int FindBalancedSplitIndex(
        IReadOnlyList<WeightedNode> items,
        int startIndex,
        int endIndex,
        double totalWeight,
        out double leadingWeight)
    {
        var targetWeight = totalWeight / 2d;
        leadingWeight = 0d;
        var splitIndex = startIndex;

        while (splitIndex < endIndex && leadingWeight + items[splitIndex].Weight <= targetWeight)
        {
            leadingWeight += items[splitIndex].Weight;
            splitIndex++;
        }

        if (splitIndex == startIndex)
        {
            leadingWeight = items[startIndex].Weight;
            return startIndex + 1;
        }

        if (splitIndex < endIndex)
        {
            var includedWeight = leadingWeight + items[splitIndex].Weight;
            if (Math.Abs(targetWeight - includedWeight) < Math.Abs(targetWeight - leadingWeight))
            {
                leadingWeight = includedWeight;
                splitIndex++;
            }
        }

        return splitIndex;
    }

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

        var sum = row.Sum(item => item.Area);
        var max = row.Max(item => item.Area);
        var min = row.Min(item => item.Area);

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

    private static double GetWeight(ProjectNode node, AnalysisMetric metric) =>
        metric switch
        {
            AnalysisMetric.TotalLines => node.Metrics.TotalLines,
            AnalysisMetric.NonEmptyLines => node.Metrics.NonEmptyLines,
            _ => node.Metrics.Tokens,
        };

    private sealed record WeightedNode(ProjectNode Node, double Weight)
    {
        public double Area { get; init; }
    }
}
