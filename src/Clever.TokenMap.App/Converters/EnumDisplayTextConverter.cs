using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Infrastructure.Settings;

namespace Clever.TokenMap.App.Converters;

public sealed class EnumDisplayTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            AnalysisMetric.Tokens => "Tokens",
            AnalysisMetric.TotalLines => "Total lines",
            AnalysisMetric.NonEmptyLines => "Non-empty lines",
            TokenProfile.O200KBase => "o200k_base",
            TokenProfile.Cl100KBase => "cl100k_base",
            TokenProfile.P50KBase => "p50k_base",
            ThemePreference.System => "System",
            ThemePreference.Light => "Light",
            ThemePreference.Dark => "Dark",
            AppLogLevel.Trace => "Trace",
            AppLogLevel.Debug => "Debug",
            AppLogLevel.Information => "Information",
            AppLogLevel.Warning => "Warning",
            AppLogLevel.Error => "Error",
            AppLogLevel.Critical => "Critical",
            _ => value?.ToString(),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
