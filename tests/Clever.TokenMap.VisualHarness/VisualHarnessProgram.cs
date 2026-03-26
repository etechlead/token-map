using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Headless;

internal static class VisualHarnessProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || string.Equals(args[0], "capture-palettes", StringComparison.OrdinalIgnoreCase))
        {
            var options = CaptureOptions.ParseCapturePalettes(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CaptureAsync(options, JsonOptions));
        }

        if (string.Equals(args[0], "capture", StringComparison.OrdinalIgnoreCase))
        {
            var options = CaptureOptions.ParseCapture(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CaptureAsync(options, JsonOptions));
        }

        if (string.Equals(args[0], "compare", StringComparison.OrdinalIgnoreCase))
        {
            var options = CompareOptions.Parse(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CompareAsync(options, JsonOptions));
        }

        PrintUsage();
        return 1;
    }

    private static async Task<int> RunWithSessionAsync(Func<Task<int>> action)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(VisualHarnessAppBuilder));
        return await session.Dispatch(action, CancellationToken.None);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tests/Clever.TokenMap.VisualHarness -- capture [--source repo|demo] [--project-root DIR] [--output-dir DIR] [--theme dark|light|system] [--metric tokens|lines|size] [--surface main|settings|treemap|all] [--palette plain|weighted|studio|all] [--compare] [--window-width N] [--window-height N] [--treemap-width N] [--treemap-height N]");
        Console.WriteLine("  dotnet run --project tests/Clever.TokenMap.VisualHarness -- capture-palettes [--source repo|demo] [--project-root DIR] [--output-dir DIR] [--theme dark|light|system] [--metric tokens|lines|size] [--surface main|settings|treemap|all] [--palette plain|weighted|studio|all] [--skip-compare]");
        Console.WriteLine("  dotnet run --project tests/Clever.TokenMap.VisualHarness -- compare --left FILE --right FILE [--output-dir DIR]");
    }
}
