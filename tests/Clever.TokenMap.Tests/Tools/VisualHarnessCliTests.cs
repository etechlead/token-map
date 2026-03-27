using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.VisualHarness;

namespace Clever.TokenMap.Tests.Tools;

public sealed class VisualHarnessCliTests
{
    [Fact]
    public void CaptureHelp_IncludesOptionMetadataFromSharedSpec()
    {
        var help = VisualHarnessCli.FormatCommandHelp(VisualHarnessCli.Capture.Command);

        Assert.Contains("--surface SURFACES", help);
        Assert.Contains("Allowed: main, settings, share, treemap, all", help);
        Assert.Contains("Allowed: plain, weighted, studio, all", help);
        Assert.Contains("Default: main", help);
        Assert.DoesNotContain("--skip-compare", help);
    }

    [Fact]
    public void CapturePalettesHelp_ReflectsCommandSpecificDefaults()
    {
        var help = VisualHarnessCli.FormatCommandHelp(VisualHarnessCli.CapturePalettes.Command);

        Assert.Contains("--skip-compare", help);
        Assert.Contains("Default: main,treemap", help);
        Assert.Contains("Default: weighted,studio,plain", help);
    }

    [Fact]
    public void CaptureOptions_ParseCapture_UsesCommandDefaultsFromSharedSpec()
    {
        var options = CaptureOptions.ParseCapture(["capture"]);

        Assert.Equal(ThemePreference.Dark, options.ThemePreference);
        Assert.Equal(AnalysisMetric.Tokens, options.Metric);
        Assert.Equal([CaptureSurface.Main], options.Surfaces);
        Assert.Equal([TreemapPalette.Weighted], options.Palettes);
        Assert.False(options.GenerateComparisons);
        Assert.Equal(1600, options.WindowSize.Width);
        Assert.Equal(1000, options.WindowSize.Height);
        Assert.Equal(1320, options.TreemapSize.Width);
        Assert.Equal(820, options.TreemapSize.Height);
    }

    [Fact]
    public void CaptureOptions_ParseCapturePalettes_UsesCommandDefaultsFromSharedSpec()
    {
        var options = CaptureOptions.ParseCapturePalettes([]);

        Assert.Equal([CaptureSurface.Main, CaptureSurface.Treemap], options.Surfaces);
        Assert.Equal([TreemapPalette.Weighted, TreemapPalette.Studio, TreemapPalette.Plain], options.Palettes);
        Assert.True(options.GenerateComparisons);
    }
}
