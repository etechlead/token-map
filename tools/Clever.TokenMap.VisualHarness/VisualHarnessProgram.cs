using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Headless;

namespace Clever.TokenMap.VisualHarness;

internal static class VisualHarnessProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0 && VisualHarnessCli.IsHelpToken(args[0]))
        {
            if (args.Length > 1 && VisualHarnessCli.TryGetCommand(args[1]) is { } helpCommand)
            {
                Console.WriteLine(VisualHarnessCli.FormatCommandHelp(helpCommand));
                return 0;
            }

            Console.WriteLine(VisualHarnessCli.FormatGeneralHelp());
            return 0;
        }

        if (args.Length == 0 || string.Equals(args[0], VisualHarnessCli.CapturePalettes.Command.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (VisualHarnessCli.ContainsHelpToken(args.Skip(1)))
            {
                Console.WriteLine(VisualHarnessCli.FormatCommandHelp(VisualHarnessCli.CapturePalettes.Command));
                return 0;
            }

            var options = CaptureOptions.ParseCapturePalettes(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CaptureAsync(options, JsonOptions));
        }

        if (string.Equals(args[0], VisualHarnessCli.Capture.Command.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (VisualHarnessCli.ContainsHelpToken(args.Skip(1)))
            {
                Console.WriteLine(VisualHarnessCli.FormatCommandHelp(VisualHarnessCli.Capture.Command));
                return 0;
            }

            var options = CaptureOptions.ParseCapture(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CaptureAsync(options, JsonOptions));
        }

        if (string.Equals(args[0], VisualHarnessCli.Compare.Command.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (VisualHarnessCli.ContainsHelpToken(args.Skip(1)))
            {
                Console.WriteLine(VisualHarnessCli.FormatCommandHelp(VisualHarnessCli.Compare.Command));
                return 0;
            }

            var options = CompareOptions.Parse(args);
            return await RunWithSessionAsync(() => VisualCaptureRunner.CompareAsync(options, JsonOptions));
        }

        Console.Error.WriteLine($"Unknown command: {args[0]}");
        Console.Error.WriteLine();
        Console.Error.WriteLine(VisualHarnessCli.FormatGeneralHelp());
        return 1;
    }

    private static async Task<int> RunWithSessionAsync(Func<Task<int>> action)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(VisualHarnessAppBuilder));
        return await session.Dispatch(action, CancellationToken.None);
    }
}
