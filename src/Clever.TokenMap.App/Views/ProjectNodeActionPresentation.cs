using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Clever.TokenMap.App.State;

namespace Clever.TokenMap.App.Views;

internal static class ProjectNodeActionPresentation
{
    public const string OpenIconResourceKey = FluentIconGeometry.FolderOpen16Regular;
    public const string PreviewIconResourceKey = FluentIconGeometry.Eye16Regular;
    public const string RefactorPromptIconResourceKey = FluentIconGeometry.ChatSparkle16Regular;
    public const string RevealIconResourceKey = FluentIconGeometry.DesktopMac16Regular;
    public const string SetAsTreemapRootIconResourceKey = FluentIconGeometry.TargetArrow16Regular;
    public const string ExcludeFromScanIconResourceKey = FluentIconGeometry.SubtractCircle16Regular;
    public const string CopyFullPathIconResourceKey = FluentIconGeometry.DocumentCopy16Regular;

    public static string GetOpenHeader(LocalizationState? localization) => localization?.OpenAction ?? "Open";

    public static string GetPreviewHeader(LocalizationState? localization) => localization?.PreviewAction ?? "Preview";

    public static string GetRefactorPromptHeader(LocalizationState? localization) => localization?.RefactorPromptAction ?? "Refactor Prompt";

    public static string GetSetAsTreemapRootHeader(LocalizationState? localization) => localization?.SetAsTreemapRootAction ?? "Set as Treemap Root";

    public static string GetExcludeFromScanHeader(LocalizationState? localization) => localization?.ExcludeFromScanAction ?? "Exclude from Scan";

    public static string GetCopyFullPathHeader(LocalizationState? localization) => localization?.CopyFullPathAction ?? "Copy Full Path";

    public static string GetCopyRelativePathHeader(LocalizationState? localization) => localization?.CopyRelativePathAction ?? "Copy Relative Path";

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
