using System;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
namespace Clever.TokenMap.App.Views;

internal static class FluentIconGeometry
{
    public const string ArrowSortDown16Regular = "FluentArrowSortDown16RegularGeometry";
    public const string ArrowSortUp16Regular = "FluentArrowSortUp16RegularGeometry";
    public const string DesktopMac16Regular = "FluentDesktopMac16RegularGeometry";
    public const string DocumentCopy16Regular = "FluentDocumentCopy16RegularGeometry";
    public const string FolderOpen16Regular = "FluentFolderOpen16RegularGeometry";
    public const string SubtractCircle16Regular = "FluentSubtractCircle16RegularGeometry";
    public const string TargetArrow16Regular = "FluentTargetArrow16RegularGeometry";

    public static Path CreatePathIcon(string resourceKey, string className, double width, double height, Thickness? margin = null)
    {
        var application = Application.Current;
        if (application is null ||
            application.TryGetResource(resourceKey, application.ActualThemeVariant, out var resource) != true ||
            resource is not Geometry geometry)
        {
            throw new InvalidOperationException($"Missing geometry resource '{resourceKey}'.");
        }

        var icon = new Path
        {
            Data = geometry,
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false,
        };

        if (margin is not null)
        {
            icon.Margin = margin.Value;
        }

        icon.Classes.Add(className);
        return icon;
    }
}
