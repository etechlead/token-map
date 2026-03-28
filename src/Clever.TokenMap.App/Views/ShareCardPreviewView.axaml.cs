using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Clever.TokenMap.App.Views;

public partial class ShareCardPreviewView : UserControl
{
    private const double SecondaryMetricsDefaultColumnWidth = 102d;
    private const double SecondaryMetricsDividerWidth = 28d;
    private const double SecondaryMetricsFallbackTotalWidth = 232d;

    private readonly TextBlock? _lineValueText;
    private readonly TextBlock? _fileValueText;
    private readonly Grid? _secondaryMetricsGrid;
    private double _lastLineMetricWidth = double.NaN;
    private double _lastFileMetricWidth = double.NaN;

    public ShareCardPreviewView()
    {
        InitializeComponent();

        _lineValueText = this.FindControl<TextBlock>("ShareLineValueText");
        _fileValueText = this.FindControl<TextBlock>("ShareFileValueText");
        _secondaryMetricsGrid = this.FindControl<Grid>("ShareSecondaryMetricsGrid");

        if (_lineValueText is not null)
        {
            _lineValueText.PropertyChanged += MetricValueTextOnPropertyChanged;
        }

        if (_fileValueText is not null)
        {
            _fileValueText.PropertyChanged += MetricValueTextOnPropertyChanged;
        }

        AttachedToVisualTree += (_, _) => UpdateSecondaryMetricWidths();
        LayoutUpdated += (_, _) => UpdateSecondaryMetricWidths();
    }

    private void MetricValueTextOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBlock.TextProperty)
        {
            UpdateSecondaryMetricWidths();
        }
    }

    private void UpdateSecondaryMetricWidths()
    {
        if (_lineValueText is null ||
            _fileValueText is null ||
            _secondaryMetricsGrid is null ||
            _secondaryMetricsGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var lineWidth = MeasureNaturalTextWidth(_lineValueText);
        var fileWidth = MeasureNaturalTextWidth(_fileValueText);
        var totalAvailableWidth = Math.Max(
            1d,
            (_secondaryMetricsGrid.Bounds.Width > 0 ? _secondaryMetricsGrid.Bounds.Width : SecondaryMetricsFallbackTotalWidth) - SecondaryMetricsDividerWidth);

        if (lineWidth <= 0 || fileWidth <= 0 || lineWidth + fileWidth <= totalAvailableWidth)
        {
            SetSecondaryMetricColumnWidths(SecondaryMetricsDefaultColumnWidth, SecondaryMetricsDefaultColumnWidth);
            return;
        }

        var combinedWidth = lineWidth + fileWidth;
        var lineColumnWidth = Math.Round(totalAvailableWidth * (lineWidth / combinedWidth), 2);
        var fileColumnWidth = Math.Round(totalAvailableWidth - lineColumnWidth, 2);
        SetSecondaryMetricColumnWidths(lineColumnWidth, fileColumnWidth);
    }

    private void SetSecondaryMetricColumnWidths(double lineWidth, double fileWidth)
    {
        if (_secondaryMetricsGrid is null || _secondaryMetricsGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        if (Math.Abs(_lastLineMetricWidth - lineWidth) < 0.1 &&
            Math.Abs(_lastFileMetricWidth - fileWidth) < 0.1)
        {
            return;
        }

        _secondaryMetricsGrid.ColumnDefinitions[0].Width = new GridLength(lineWidth, GridUnitType.Pixel);
        _secondaryMetricsGrid.ColumnDefinitions[2].Width = new GridLength(fileWidth, GridUnitType.Pixel);
        _lastLineMetricWidth = lineWidth;
        _lastFileMetricWidth = fileWidth;
    }

    private static double MeasureNaturalTextWidth(TextBlock textBlock)
    {
        if (string.IsNullOrWhiteSpace(textBlock.Text))
        {
            return 0d;
        }

        var formattedText = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentCulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight),
            textBlock.FontSize,
            textBlock.Foreground ?? Brushes.Black);

        return formattedText.WidthIncludingTrailingWhitespace;
    }
}
