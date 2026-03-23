using Clever.TokenMap.Core.Enums;

internal enum CaptureSource
{
    Repo,
    Demo,
}

internal enum CaptureSurface
{
    Main,
    Settings,
    Treemap,
}

internal sealed record CaptureCanvasSize(int Width, int Height);

internal sealed record CaptureOptions(
    string OutputDirectory,
    ThemePreference ThemePreference,
    AnalysisMetric Metric,
    CaptureSource Source,
    string ProjectRoot,
    IReadOnlyList<TreemapPalette> Palettes,
    IReadOnlyList<CaptureSurface> Surfaces,
    bool GenerateComparisons,
    CaptureCanvasSize WindowSize,
    CaptureCanvasSize TreemapSize)
{
    public static CaptureOptions ParseCapture(string[] args)
    {
        return Parse(
            args,
            defaultPalettes: [TreemapPalette.Weighted],
            defaultSurfaces: [CaptureSurface.Main],
            generateComparisonsByDefault: false);
    }

    public static CaptureOptions ParseCapturePalettes(string[] args)
    {
        return Parse(
            args,
            defaultPalettes: [TreemapPalette.Weighted, TreemapPalette.Studio, TreemapPalette.Classic],
            defaultSurfaces: [CaptureSurface.Main, CaptureSurface.Treemap],
            generateComparisonsByDefault: true);
    }

    private static CaptureOptions Parse(
        string[] args,
        IReadOnlyList<TreemapPalette> defaultPalettes,
        IReadOnlyList<CaptureSurface> defaultSurfaces,
        bool generateComparisonsByDefault)
    {
        var source = CliParsing.ParseCaptureSource(CliParsing.GetOptionValue(args, "--source") ?? "repo");
        var projectRoot = Path.GetFullPath(CliParsing.GetOptionValue(args, "--project-root") ?? Directory.GetCurrentDirectory());
        var outputDirectory = Path.GetFullPath(
            CliParsing.GetOptionValue(args, "--output-dir")
            ?? CliParsing.GetDefaultArtifactDirectory("visual-harness"));
        var theme = CliParsing.ParseThemePreference(CliParsing.GetOptionValue(args, "--theme") ?? "dark");
        var metric = CliParsing.ParseMetric(CliParsing.GetOptionValue(args, "--metric") ?? "tokens");
        var palettes = CliParsing.ParsePalettes(CliParsing.GetOptionValue(args, "--palette") ?? string.Join(",", defaultPalettes));
        var surfaces = CliParsing.ParseCaptureSurfaces(CliParsing.GetOptionValue(args, "--surface") ?? string.Join(",", defaultSurfaces));
        var generateComparisons = generateComparisonsByDefault && !CliParsing.HasFlag(args, "--skip-compare")
            || CliParsing.HasFlag(args, "--compare");
        var windowSize = new CaptureCanvasSize(
            CliParsing.GetOptionalIntValue(args, "--window-width", 1600),
            CliParsing.GetOptionalIntValue(args, "--window-height", 1000));
        var treemapSize = new CaptureCanvasSize(
            CliParsing.GetOptionalIntValue(args, "--treemap-width", 1320),
            CliParsing.GetOptionalIntValue(args, "--treemap-height", 820));

        return new CaptureOptions(
            outputDirectory,
            theme,
            metric,
            source,
            projectRoot,
            palettes,
            surfaces,
            generateComparisons,
            windowSize,
            treemapSize);
    }
}

internal sealed record CompareOptions(string LeftPath, string RightPath, string OutputDirectory)
{
    public static CompareOptions Parse(string[] args)
    {
        var leftPath = CliParsing.GetRequiredOptionValue(args, "--left");
        var rightPath = CliParsing.GetRequiredOptionValue(args, "--right");
        var outputDirectory = CliParsing.GetOptionValue(args, "--output-dir")
            ?? CliParsing.GetDefaultArtifactDirectory("visual-compare");
        return new CompareOptions(Path.GetFullPath(leftPath), Path.GetFullPath(rightPath), Path.GetFullPath(outputDirectory));
    }
}
