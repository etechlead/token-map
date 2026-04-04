using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Clever.TokenMap.App.Views;

internal static class ProjectNodeActionPresentation
{
    public const string OpenHeader = "Open";
    public const string PreviewHeader = "Preview";
    public const string SetAsTreemapRootHeader = "Set as Treemap Root";
    public const string ExcludeFromScanHeader = "Exclude from Scan";
    public const string CopyFullPathHeader = "Copy Full Path";
    public const string CopyRelativePathHeader = "Copy Relative Path";

    public const string OpenIconResourceKey = FluentIconGeometry.FolderOpen16Regular;
    public const string PreviewIconResourceKey = FluentIconGeometry.Eye16Regular;
    public const string RevealIconResourceKey = FluentIconGeometry.DesktopMac16Regular;
    public const string SetAsTreemapRootIconResourceKey = FluentIconGeometry.TargetArrow16Regular;
    public const string ExcludeFromScanIconResourceKey = FluentIconGeometry.SubtractCircle16Regular;
    public const string CopyFullPathIconResourceKey = FluentIconGeometry.DocumentCopy16Regular;

    public static Control? CreateContextMenuIcon(string? iconResourceKey) =>
        string.IsNullOrWhiteSpace(iconResourceKey)
            ? null
            : FluentIconGeometry.CreatePathIcon(iconResourceKey, "context-menu-icon", 18, 18);

    public static object CreateActionButtonContent(string header, string? iconResourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);

        if (string.IsNullOrWhiteSpace(iconResourceKey))
        {
            return header;
        }

        var panel = new StackPanel();
        panel.Classes.Add("action-button-content");
        panel.Children.Add(CreateActionButtonIcon(iconResourceKey));
        panel.Children.Add(new TextBlock
        {
            Text = header,
            IsHitTestVisible = false,
        });

        return panel;
    }

    private static Path CreateActionButtonIcon(string iconResourceKey) =>
        FluentIconGeometry.CreatePathIcon(iconResourceKey, "action-button-icon", 16, 16);
}
