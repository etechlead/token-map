internal sealed record CaptureRunReport(
    string Scenario,
    string Theme,
    string Metric,
    string ProjectRoot,
    string OutputDirectory,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<CaptureImageArtifact> Images,
    IReadOnlyList<CaptureComparisonArtifact> Comparisons);

internal sealed record CaptureImageArtifact(
    string Surface,
    string Palette,
    string ImagePath,
    int Width,
    int Height,
    bool SettingsOpen);

internal sealed record CaptureComparisonArtifact(
    string Surface,
    string LeftPalette,
    string RightPalette,
    string DiffImagePath,
    ImageComparisonResult Result);

internal sealed record ComparisonReport(
    string LeftPath,
    string RightPath,
    string DiffPath,
    string OutputDirectory,
    DateTimeOffset GeneratedAtUtc,
    ImageComparisonResult Result);

internal sealed record ImageComparisonResult(
    int Width,
    int Height,
    int ChangedPixels,
    double ChangedPixelRatio,
    double MeanAbsoluteChannelDelta,
    byte MaxAbsoluteChannelDelta);
