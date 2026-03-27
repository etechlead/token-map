using Avalonia;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

internal static class TreemapVisualRules
{
    private const double NodeInset = 1;
    private const double DirectoryInset = 2;
    private const double FileLabelInsetX = 4;
    private const double FileLabelInsetY = 3;
    private const double DirectoryLabelInsetX = 3;
    private const double DirectoryLabelInsetY = 0;

    public static double GetDirectoryHeaderHeight(ProjectNode node, Rect bounds, bool includeHeader = true)
    {
        if (!includeHeader || node.Kind != ProjectNodeKind.Directory)
        {
            return 0;
        }

        if (bounds.Width >= 110 && bounds.Height >= 42)
        {
            return 16;
        }

        if (bounds.Width >= 80 && bounds.Height >= 30)
        {
            return 12;
        }

        return 0;
    }

    public static Rect GetContentBounds(ProjectNode node, Rect bounds, bool includeDirectoryHeader = true)
    {
        if (!includeDirectoryHeader && node.Kind != ProjectNodeKind.File)
        {
            return bounds;
        }

        var innerBounds = Inset(bounds, GetNodeInset(node));
        var headerHeight = GetDirectoryHeaderHeight(node, bounds, includeDirectoryHeader);
        if (headerHeight <= 0)
        {
            return innerBounds;
        }

        return innerBounds.Height <= headerHeight
            ? new Rect(innerBounds.X, innerBounds.Bottom, innerBounds.Width, 0)
            : new Rect(innerBounds.X, innerBounds.Y + headerHeight, innerBounds.Width, innerBounds.Height - headerHeight);
    }

    public static Rect GetHeaderBounds(ProjectNode node, Rect bounds, bool includeDirectoryHeader = true)
    {
        var headerHeight = GetDirectoryHeaderHeight(node, bounds, includeDirectoryHeader);
        if (headerHeight <= 0)
        {
            return default;
        }

        var innerBounds = Inset(bounds, GetNodeInset(node));
        return new Rect(innerBounds.X, innerBounds.Y, innerBounds.Width, headerHeight);
    }

    public static Rect GetLabelBounds(ProjectNode node, Rect bounds)
    {
        var isDirectory = node.Kind == ProjectNodeKind.Directory;
        var sourceBounds = isDirectory
            ? GetHeaderBounds(node, bounds)
            : Inset(bounds, NodeInset);
        var insetX = isDirectory ? DirectoryLabelInsetX : FileLabelInsetX;
        var insetY = isDirectory ? DirectoryLabelInsetY : FileLabelInsetY;

        if (sourceBounds.Width <= insetX * 2 || sourceBounds.Height <= insetY * 2)
        {
            return default;
        }

        return new Rect(
            sourceBounds.X + insetX,
            sourceBounds.Y + insetY,
            sourceBounds.Width - (insetX * 2),
            sourceBounds.Height - (insetY * 2));
    }

    public static bool CanDrawLabel(ProjectNode node, Rect bounds)
    {
        var labelBounds = GetLabelBounds(node, bounds);
        if (labelBounds.Width <= 0 || labelBounds.Height <= 0)
        {
            return false;
        }

        return node.Kind == ProjectNodeKind.Directory
            ? labelBounds.Width >= 70 && labelBounds.Height >= 10
            : labelBounds.Width >= 64 && labelBounds.Height >= 16;
    }

    public static double GetLabelFontSize(ProjectNode node) =>
        node.Kind == ProjectNodeKind.Directory ? 10 : 12;

    public static Rect Inset(Rect rect, double inset) =>
        rect.Width <= inset * 2 || rect.Height <= inset * 2
            ? rect
            : new Rect(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);

    private static double GetNodeInset(ProjectNode node) =>
        node.Kind == ProjectNodeKind.Directory
            ? DirectoryInset
            : NodeInset;
}
