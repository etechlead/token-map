using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.VisualHarness;

internal enum CaptureSource
{
    Repo,
    Demo,
}

internal enum CaptureSurface
{
    Main,
    Settings,
    Share,
    Treemap,
}

internal sealed record CaptureCanvasSize(int Width, int Height);

internal sealed record ShareMetricOverrides(
    long? Tokens,
    int? Lines,
    int? Files);

internal sealed record CaptureOptions(
    string OutputDirectory,
    ThemePreference ThemePreference,
    MetricId Metric,
    CaptureSource Source,
    string ProjectRoot,
    IReadOnlyList<TreemapPalette> Palettes,
    IReadOnlyList<CaptureSurface> Surfaces,
    bool GenerateComparisons,
    CaptureCanvasSize WindowSize,
    CaptureCanvasSize TreemapSize,
    ShareMetricOverrides ShareMetrics)
{
    public static CaptureOptions ParseCapture(string[] args) => Parse(args, VisualHarnessCli.Capture);

    public static CaptureOptions ParseCapturePalettes(string[] args) => Parse(args, VisualHarnessCli.CapturePalettes);

    private static CaptureOptions Parse(string[] args, CaptureCommandDefinition definition)
    {
        var generateComparisons = definition.GenerateComparisonsByDefault;
        if (definition.SkipCompareOption?.IsSet(args) == true)
        {
            generateComparisons = false;
        }

        if (definition.CompareOption?.IsSet(args) == true)
        {
            generateComparisons = true;
        }

        var windowSize = new CaptureCanvasSize(
            definition.WindowWidthOption.GetValue(args),
            definition.WindowHeightOption.GetValue(args));
        var treemapSize = new CaptureCanvasSize(
            definition.TreemapWidthOption.GetValue(args),
            definition.TreemapHeightOption.GetValue(args));

        return new CaptureOptions(
            definition.OutputDirectoryOption.GetValue(args),
            definition.ThemeOption.GetValue(args),
            definition.MetricOption.GetValue(args),
            definition.SourceOption.GetValue(args),
            definition.ProjectRootOption.GetValue(args),
            definition.PaletteOption.GetValue(args),
            definition.SurfaceOption.GetValue(args),
            generateComparisons,
            windowSize,
            treemapSize,
            new ShareMetricOverrides(
                CliParsing.GetOptionValue(args, definition.ShareTokensOption.Name) is { } shareTokens ? CliParsing.ParseLong(shareTokens) : null,
                CliParsing.GetOptionValue(args, definition.ShareLinesOption.Name) is { } shareLines ? CliParsing.ParseInt(shareLines) : null,
                CliParsing.GetOptionValue(args, definition.ShareFilesOption.Name) is { } shareFiles ? CliParsing.ParseInt(shareFiles) : null));
    }
}

internal sealed record CompareOptions(string LeftPath, string RightPath, string OutputDirectory)
{
    public static CompareOptions Parse(string[] args)
    {
        return new CompareOptions(
            VisualHarnessCli.Compare.LeftOption.GetValue(args),
            VisualHarnessCli.Compare.RightOption.GetValue(args),
            VisualHarnessCli.Compare.OutputDirectoryOption.GetValue(args));
    }
}
