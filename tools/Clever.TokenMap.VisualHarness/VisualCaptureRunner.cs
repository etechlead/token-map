using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.App.Views;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.VisualHarness;

internal static class VisualCaptureRunner
{
    public static async Task<int> CaptureAsync(CaptureOptions options, JsonSerializerOptions jsonOptions)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        SetRequestedTheme(options.ThemePreference);
        var snapshot = options.Source switch
        {
            CaptureSource.Demo => PaletteDemoSnapshotFactory.Create(),
            _ => await AnalyzeProjectAsync(options),
        };

        var images = new List<CaptureImageArtifact>();
        var imagePaths = new Dictionary<(CaptureSurface Surface, TreemapPalette Palette), string>();

        foreach (var surface in options.Surfaces)
        {
            foreach (var palette in options.Palettes)
            {
                using var bitmap = await CaptureSurfaceAsync(snapshot, options, surface, palette);
                var imagePath = Path.Combine(
                    options.OutputDirectory,
                    $"{surface.ToString().ToLowerInvariant()}.{palette.ToString().ToLowerInvariant()}.png");
                bitmap.Save(imagePath);

                images.Add(new CaptureImageArtifact(
                    Surface: surface.ToString(),
                    Palette: palette.ToString(),
                    ImagePath: imagePath,
                    Width: bitmap.PixelSize.Width,
                    Height: bitmap.PixelSize.Height,
                    SettingsOpen: surface == CaptureSurface.Settings));
                imagePaths[(surface, palette)] = imagePath;
            }
        }

        var comparisons = new List<CaptureComparisonArtifact>();
        if (options.GenerateComparisons && options.Palettes.Count > 1)
        {
            foreach (var surface in options.Surfaces)
            {
                var baselinePalette = options.Palettes.Contains(TreemapPalette.Weighted)
                    ? TreemapPalette.Weighted
                    : options.Palettes[0];

                foreach (var palette in options.Palettes.Where(candidate => candidate != baselinePalette))
                {
                    using var left = new Bitmap(imagePaths[(surface, baselinePalette)]);
                    using var right = new Bitmap(imagePaths[(surface, palette)]);
                    var diffPath = Path.Combine(
                        options.OutputDirectory,
                        $"{surface.ToString().ToLowerInvariant()}.{baselinePalette.ToString().ToLowerInvariant()}-vs-{palette.ToString().ToLowerInvariant()}.diff.png");
                    var result = CompareBitmaps(left, right, diffPath);
                    comparisons.Add(new CaptureComparisonArtifact(
                        Surface: surface.ToString(),
                        LeftPalette: baselinePalette.ToString(),
                        RightPalette: palette.ToString(),
                        DiffImagePath: diffPath,
                        Result: result));
                }
            }
        }

        var report = new CaptureRunReport(
            Scenario: options.Source.ToString().ToLowerInvariant(),
            Theme: options.ThemePreference.ToString(),
            Metric: CliParsing.GetMetricToken(options.Metric),
            ProjectRoot: options.ProjectRoot,
            OutputDirectory: options.OutputDirectory,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Images: images,
            Comparisons: comparisons);
        var reportPath = Path.Combine(options.OutputDirectory, "report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, jsonOptions));

        Console.WriteLine($"Saved {images.Count} captures to {options.OutputDirectory}");
        Console.WriteLine($"Scenario: {options.Source}, project root: {options.ProjectRoot}");
        foreach (var image in images)
        {
            Console.WriteLine($"  {image.Surface}/{image.Palette}: {image.ImagePath}");
        }

        foreach (var comparison in comparisons)
        {
            Console.WriteLine($"  compare {comparison.Surface}: {comparison.LeftPalette} vs {comparison.RightPalette} -> {comparison.Result.ChangedPixels:N0} px ({comparison.Result.ChangedPixelRatio:P2})");
        }

        Console.WriteLine($"Report: {reportPath}");
        return 0;
    }

    public static async Task<int> CompareAsync(CompareOptions options, JsonSerializerOptions jsonOptions)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        SetRequestedTheme(ThemePreference.Dark);

        using var left = new Bitmap(options.LeftPath);
        using var right = new Bitmap(options.RightPath);
        var diffPath = Path.Combine(options.OutputDirectory, "diff.png");
        var comparison = CompareBitmaps(left, right, diffPath);
        var report = new ComparisonReport(
            LeftPath: options.LeftPath,
            RightPath: options.RightPath,
            DiffPath: diffPath,
            OutputDirectory: options.OutputDirectory,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Result: comparison);

        var reportPath = Path.Combine(options.OutputDirectory, "report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, jsonOptions));

        Console.WriteLine($"Compared {options.LeftPath}");
        Console.WriteLine($"     and {options.RightPath}");
        Console.WriteLine($"Changed pixels: {comparison.ChangedPixels:N0} ({comparison.ChangedPixelRatio:P2})");
        Console.WriteLine($"Mean channel delta: {comparison.MeanAbsoluteChannelDelta:F2}");
        Console.WriteLine($"Diff image: {diffPath}");
        Console.WriteLine($"Report: {reportPath}");
        return 0;
    }

    private static async Task<ProjectSnapshot> AnalyzeProjectAsync(CaptureOptions options)
    {
        var userExcludes = new List<string>
        {
            ".git",
            ".artifacts/visual-harness",
            ".artifacts/visual-compare",
        };

        if (TryGetRelativeOutputExclude(options.ProjectRoot, options.OutputDirectory) is { } outputExclude &&
            !userExcludes.Contains(outputExclude, StringComparer.OrdinalIgnoreCase))
        {
            userExcludes.Add(outputExclude);
        }

        var analyzer = HarnessComposition.CreateDefaultProjectAnalyzer();
        var scanOptions = new ScanOptions
        {
            RespectGitIgnore = true,
            UseGlobalExcludes = true,
            GlobalExcludes = [.. GlobalExcludeDefaults.DefaultEntries, .. userExcludes],
        };

        Console.WriteLine($"Analyzing project for visual capture: {options.ProjectRoot}");
        return await analyzer.AnalyzeAsync(options.ProjectRoot, scanOptions, progress: null, CancellationToken.None);
    }

    private static string? TryGetRelativeOutputExclude(string projectRoot, string outputDirectory)
    {
        var fullProjectRoot = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!fullOutputDirectory.StartsWith(fullProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(fullProjectRoot, fullOutputDirectory).Replace('\\', '/');
        return relativePath.StartsWith("..", StringComparison.Ordinal)
            ? null
            : relativePath;
    }

    private static Task<WriteableBitmap> CaptureSurfaceAsync(
        ProjectSnapshot snapshot,
        CaptureOptions options,
        CaptureSurface surface,
        TreemapPalette palette)
    {
        return surface switch
        {
            CaptureSurface.Main => CaptureMainWindowAsync(snapshot, options, palette, settingsOpen: false),
            CaptureSurface.Settings => CaptureMainWindowAsync(snapshot, options, palette, settingsOpen: true),
            CaptureSurface.Share => CaptureShareWindowAsync(snapshot, options, palette),
            CaptureSurface.Treemap => CaptureStandaloneTreemapAsync(snapshot, options, palette),
            _ => throw new InvalidOperationException($"Unsupported surface '{surface}'."),
        };
    }

    private static async Task<WriteableBitmap> CaptureMainWindowAsync(
        ProjectSnapshot snapshot,
        CaptureOptions options,
        TreemapPalette palette,
        bool settingsOpen)
    {
        var settingsState = new SettingsState
        {
            SelectedMetric = options.Metric,
            SelectedTreemapPalette = palette,
        };
        var settingsCoordinator = new InlineSettingsCoordinator(settingsState);
        var folderPathService = new ExistingFolderPathService();

        var analysisSessionController = HarnessComposition.CreateAnalysisSessionController(
            new SnapshotProjectAnalyzer(snapshot),
            new FixedFolderPickerService(snapshot.RootPath),
            folderPathService,
            settingsCoordinator);
        await analysisSessionController.OpenFolderAsync(snapshot.RootPath, ScanOptions.Default);

        var viewModel = HarnessComposition.CreateMainWindowViewModel(
            analysisSessionController,
            settingsCoordinator,
            folderPathService,
            new NoOpPathShellService());
        ApplyThreshold(viewModel, options.Threshold);
        viewModel.IsSettingsOpen = settingsOpen;

        var window = new MainWindow
        {
            DataContext = viewModel,
            Width = options.WindowSize.Width,
            Height = options.WindowSize.Height,
        };

        return await CaptureWindowAsync(window, "MainWindow did not render a frame.");
    }

    private static async Task<WriteableBitmap> CaptureShareWindowAsync(
        ProjectSnapshot snapshot,
        CaptureOptions options,
        TreemapPalette palette)
    {
        snapshot = ApplyShareMetricOverrides(snapshot, options.ShareMetrics);

        var settingsState = new SettingsState
        {
            SelectedMetric = options.Metric,
            SelectedTreemapPalette = palette,
        };
        var settingsCoordinator = new InlineSettingsCoordinator(settingsState);
        var folderPathService = new ExistingFolderPathService();

        var analysisSessionController = HarnessComposition.CreateAnalysisSessionController(
            new SnapshotProjectAnalyzer(snapshot),
            new FixedFolderPickerService(snapshot.RootPath),
            folderPathService,
            settingsCoordinator);
        await analysisSessionController.OpenFolderAsync(snapshot.RootPath, ScanOptions.Default);

        var viewModel = HarnessComposition.CreateMainWindowViewModel(
            analysisSessionController,
            settingsCoordinator,
            folderPathService,
            new NoOpPathShellService());
        ApplyThreshold(viewModel, options.Threshold);
        viewModel.OpenShareSnapshotCommand.Execute(null);

        var window = new MainWindow
        {
            DataContext = viewModel,
            Width = options.WindowSize.Width,
            Height = options.WindowSize.Height,
        };

        return await CaptureWindowAsync(window, "Share window did not render a frame.");
    }

    private static ProjectSnapshot ApplyShareMetricOverrides(ProjectSnapshot snapshot, ShareMetricOverrides overrides)
    {
        if (overrides.Tokens is null && overrides.Lines is null && overrides.Files is null)
        {
            return snapshot;
        }

        var root = snapshot.Root;
        var computedMetricValues = root.ComputedMetrics.Values.ToDictionary(entry => entry.Key, entry => entry.Value);
        if (overrides.Tokens is { } tokens)
        {
            computedMetricValues[MetricIds.Tokens] = MetricValue.From(tokens);
        }

        if (overrides.Lines is { } lines)
        {
            computedMetricValues[MetricIds.NonEmptyLines] = MetricValue.From(lines);
        }

        var updatedRoot = new ProjectNode
        {
            Id = root.Id,
            Name = root.Name,
            FullPath = root.FullPath,
            RelativePath = root.RelativePath,
            Kind = root.Kind,
            Summary = root.Summary with
            {
                DescendantFileCount = overrides.Files ?? root.Summary.DescendantFileCount,
            },
            ComputedMetrics = new MetricSet(computedMetricValues),
            SkippedReason = root.SkippedReason,
        };

        updatedRoot.Children.AddRange(root.Children);

        return new ProjectSnapshot
        {
            RootPath = snapshot.RootPath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            Options = snapshot.Options,
            Root = updatedRoot,
            Diagnostics = snapshot.Diagnostics,
        };
    }

    private static Task<WriteableBitmap> CaptureStandaloneTreemapAsync(
        ProjectSnapshot snapshot,
        CaptureOptions options,
        TreemapPalette palette)
    {
        var hostBackground = TryGetBrushColor("TokenMapAppBackgroundBrush", options.ThemePreference, Color.Parse("#15181D"));
        var panelBackground = TryGetBrushColor("TokenMapSurfaceBrush", options.ThemePreference, Color.Parse("#1C2128"));
        var treemap = new TreemapControl
        {
            Width = options.TreemapSize.Width,
            Height = options.TreemapSize.Height,
            RootNode = snapshot.Root,
            Metric = options.Metric,
            Palette = palette,
            MinimumVisibleWeight = options.Threshold ?? double.NegativeInfinity,
        };

        var host = new Border
        {
            Background = new SolidColorBrush(panelBackground),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = treemap,
        };

        var window = new Window
        {
            Width = options.TreemapSize.Width + 80,
            Height = options.TreemapSize.Height + 80,
            Background = new SolidColorBrush(hostBackground),
            Content = host,
        };

        return CaptureWindowAsync(window, "Standalone treemap window did not render a frame.");
    }

    private static void ApplyThreshold(MainWindowViewModel viewModel, double? requestedThreshold)
    {
        if (requestedThreshold is null || !viewModel.CanAdjustTreemapThreshold)
        {
            return;
        }

        var bestSliderValue = viewModel.TreemapThresholdSliderMinimum;
        var bestDifference = Math.Abs(viewModel.TreemapThresholdValue - requestedThreshold.Value);

        for (var sliderValue = viewModel.TreemapThresholdSliderMinimum; sliderValue <= viewModel.TreemapThresholdSliderMaximum; sliderValue++)
        {
            viewModel.TreemapThresholdSliderValue = sliderValue;
            var difference = Math.Abs(viewModel.TreemapThresholdValue - requestedThreshold.Value);
            if (difference < bestDifference)
            {
                bestDifference = difference;
                bestSliderValue = sliderValue;
            }

            if (difference <= 0d)
            {
                break;
            }
        }

        viewModel.TreemapThresholdSliderValue = bestSliderValue;
    }

    private static async Task<WriteableBitmap> CaptureWindowAsync(Window window, string failureMessage)
    {
        try
        {
            window.Show();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException(failureMessage);
            return frame;
        }
        finally
        {
            window.Close();
        }
    }

    private static ImageComparisonResult CompareBitmaps(Bitmap left, Bitmap right, string diffPath)
    {
        var leftBuffer = BitmapBuffer.FromBitmap(left);
        var rightBuffer = BitmapBuffer.FromBitmap(right);
        if (leftBuffer.Width != rightBuffer.Width || leftBuffer.Height != rightBuffer.Height)
        {
            throw new InvalidOperationException(
                $"Images must have the same size. Left={leftBuffer.Width}x{leftBuffer.Height}, Right={rightBuffer.Width}x{rightBuffer.Height}.");
        }

        var diffPixels = new byte[leftBuffer.RowBytes * leftBuffer.Height];
        var changedPixels = 0;
        long totalChannelDelta = 0;
        byte maxChannelDelta = 0;

        for (var y = 0; y < leftBuffer.Height; y++)
        {
            var rowOffset = y * leftBuffer.RowBytes;
            for (var x = 0; x < leftBuffer.Width; x++)
            {
                var pixelOffset = rowOffset + (x * 4);
                var bDelta = Math.Abs(leftBuffer.Pixels[pixelOffset] - rightBuffer.Pixels[pixelOffset]);
                var gDelta = Math.Abs(leftBuffer.Pixels[pixelOffset + 1] - rightBuffer.Pixels[pixelOffset + 1]);
                var rDelta = Math.Abs(leftBuffer.Pixels[pixelOffset + 2] - rightBuffer.Pixels[pixelOffset + 2]);
                var aDelta = Math.Abs(leftBuffer.Pixels[pixelOffset + 3] - rightBuffer.Pixels[pixelOffset + 3]);
                var pixelDelta = Math.Max(Math.Max(bDelta, gDelta), Math.Max(rDelta, aDelta));

                if (pixelDelta > 0)
                {
                    changedPixels++;
                }

                totalChannelDelta += bDelta + gDelta + rDelta + aDelta;
                maxChannelDelta = (byte)Math.Max(maxChannelDelta, pixelDelta);

                diffPixels[pixelOffset] = 32;
                diffPixels[pixelOffset + 1] = (byte)Math.Min(255, pixelDelta * 2);
                diffPixels[pixelOffset + 2] = (byte)Math.Min(255, pixelDelta * 4);
                diffPixels[pixelOffset + 3] = 255;
            }
        }

        using var diffBitmap = BitmapBuffer.Create(leftBuffer.Width, leftBuffer.Height, diffPixels).ToWriteableBitmap();
        diffBitmap.Save(diffPath);

        var totalPixels = leftBuffer.Width * leftBuffer.Height;
        var totalChannels = totalPixels * 4d;
        return new ImageComparisonResult(
            Width: leftBuffer.Width,
            Height: leftBuffer.Height,
            ChangedPixels: changedPixels,
            ChangedPixelRatio: totalPixels == 0 ? 0 : (double)changedPixels / totalPixels,
            MeanAbsoluteChannelDelta: totalChannels == 0 ? 0 : totalChannelDelta / totalChannels,
            MaxAbsoluteChannelDelta: maxChannelDelta);
    }

    private static Color TryGetBrushColor(string resourceKey, ThemePreference themePreference, Color fallback)
    {
        SetRequestedTheme(themePreference);
        if (Application.Current?.TryFindResource(resourceKey, out var value) == true &&
            value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        return fallback;
    }

    private static void SetRequestedTheme(ThemePreference themePreference)
    {
        Application.Current!.RequestedThemeVariant = themePreference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
