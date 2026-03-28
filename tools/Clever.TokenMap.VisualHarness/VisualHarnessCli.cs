using System.Text;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.VisualHarness;

internal static class VisualHarnessCli
{
    public const string ProjectPath = "tools/Clever.TokenMap.VisualHarness";

    public static CaptureCommandDefinition Capture { get; } = CreateCaptureDefinition(
        name: "capture",
        summary: "Capture one or more surfaces without implicit palette comparisons.",
        defaultPalettes: [TreemapPalette.Weighted],
        defaultSurfaces: [CaptureSurface.Main],
        includeCompareFlag: true,
        includeSkipCompareFlag: false,
        generateComparisonsByDefault: false,
        examples:
        [
            $"dotnet run --project {ProjectPath} -- capture --source repo --project-root . --theme light --surface main",
            $"dotnet run --project {ProjectPath} -- capture --source demo --surface treemap --palette studio --metric size",
            $"dotnet run --project {ProjectPath} -- capture --surface share --share-tokens 245000000 --share-lines 3200000 --share-files 280000",
            $"dotnet run --project {ProjectPath} -- capture --surface main,settings --palette weighted,studio --compare",
        ]);

    public static CaptureCommandDefinition CapturePalettes { get; } = CreateCaptureDefinition(
        name: "capture-palettes",
        summary: "Capture multiple palettes from the same snapshot and compare them by default.",
        defaultPalettes: [TreemapPalette.Weighted, TreemapPalette.Studio, TreemapPalette.Plain],
        defaultSurfaces: [CaptureSurface.Main, CaptureSurface.Treemap],
        includeCompareFlag: true,
        includeSkipCompareFlag: true,
        generateComparisonsByDefault: true,
        examples:
        [
            $"dotnet run --project {ProjectPath} -- capture-palettes --source repo --project-root . --metric size",
            $"dotnet run --project {ProjectPath} -- capture-palettes --theme light --surface main --skip-compare",
        ]);

    public static CompareCommandDefinition Compare { get; } = CreateCompareDefinition();

    public static IReadOnlyList<CliCommandSpec> Commands { get; } =
    [
        Capture.Command,
        CapturePalettes.Command,
        Compare.Command,
    ];

    public static bool IsHelpToken(string token) =>
        string.Equals(token, "help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);

    public static bool ContainsHelpToken(IEnumerable<string> tokens) =>
        tokens.Any(IsHelpToken);

    public static CliCommandSpec? TryGetCommand(string token) =>
        Commands.FirstOrDefault(command => string.Equals(command.Name, token, StringComparison.OrdinalIgnoreCase));

    public static string FormatGeneralHelp()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Visual Harness");
        builder.AppendLine("Headless Avalonia capture tool for TokenMap surfaces and image diffs.");
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.AppendLine($"  dotnet run --project {ProjectPath} -- help [command]");
        foreach (var command in Commands)
        {
            builder.Append("  ");
            builder.AppendLine(BuildUsage(command));
        }

        builder.AppendLine();
        builder.AppendLine("Commands:");
        var maxNameLength = Commands.Max(command => command.Name.Length);
        foreach (var command in Commands)
        {
            builder.Append("  ");
            builder.Append(command.Name.PadRight(maxNameLength));
            builder.Append("  ");
            builder.AppendLine(command.Summary);
        }

        builder.AppendLine();
        builder.AppendLine("Notes:");
        builder.AppendLine("  Running with no command keeps the existing default behavior: capture-palettes.");
        builder.AppendLine("  Use '<command> --help' or 'help <command>' for command-specific options.");
        return builder.ToString().TrimEnd();
    }

    public static string FormatCommandHelp(CliCommandSpec command)
    {
        var builder = new StringBuilder();
        builder.Append("Visual Harness - ");
        builder.AppendLine(command.Name);
        builder.AppendLine(command.Summary);
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.Append("  ");
        builder.AppendLine(BuildUsage(command));

        if (command.Options.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Options:");
            var maxSignatureLength = command.Options.Max(option => option.UsageToken.Length);
            foreach (var option in command.Options)
            {
                builder.Append("  ");
                builder.Append(option.UsageToken.PadRight(maxSignatureLength));
                builder.Append("  ");
                builder.AppendLine(option.Description);

                if (option.GetHelpMetadata() is { Length: > 0 } metadata)
                {
                    builder.Append(' ', maxSignatureLength + 4);
                    builder.AppendLine(metadata);
                }
            }
        }

        if (command.Examples.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Examples:");
            foreach (var example in command.Examples)
            {
                builder.Append("  ");
                builder.AppendLine(example);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static CaptureCommandDefinition CreateCaptureDefinition(
        string name,
        string summary,
        IReadOnlyList<TreemapPalette> defaultPalettes,
        IReadOnlyList<CaptureSurface> defaultSurfaces,
        bool includeCompareFlag,
        bool includeSkipCompareFlag,
        bool generateComparisonsByDefault,
        IReadOnlyList<string> examples)
    {
        var source = new CliValueOption<CaptureSource>(
            "--source",
            "SOURCE",
            "Choose whether to analyze a real repo or use deterministic demo data.",
            CliParsing.ParseEnumToken<CaptureSource>,
            defaultFactory: () => CaptureSource.Repo,
            defaultValueDescription: CliParsing.GetEnumToken(CaptureSource.Repo),
            allowedValues: CliParsing.GetEnumTokens(Enum.GetValues<CaptureSource>()));
        var projectRoot = new CliValueOption<string>(
            "--project-root",
            "DIR",
            "Set the repo root used when capturing from repo data.",
            Path.GetFullPath,
            defaultFactory: () => Path.GetFullPath(Directory.GetCurrentDirectory()),
            defaultValueDescription: "current working directory");
        var outputDirectory = new CliValueOption<string>(
            "--output-dir",
            "DIR",
            "Override the artifact directory for captured images and reports.",
            Path.GetFullPath,
            defaultFactory: () => CliParsing.GetDefaultArtifactDirectory("visual-harness"),
            defaultValueDescription: ".artifacts/visual-harness/<utc-timestamp>");
        var theme = new CliValueOption<ThemePreference>(
            "--theme",
            "THEME",
            "Choose the requested app theme for the capture run.",
            CliParsing.ParseEnumToken<ThemePreference>,
            defaultFactory: () => ThemePreference.Dark,
            defaultValueDescription: CliParsing.GetEnumToken(ThemePreference.Dark),
            allowedValues: CliParsing.GetEnumTokens(Enum.GetValues<ThemePreference>()));
        var metric = new CliValueOption<AnalysisMetric>(
            "--metric",
            "METRIC",
            "Select which analysis metric drives labels and treemap weighting.",
            CliParsing.ParseEnumToken<AnalysisMetric>,
            defaultFactory: () => AnalysisMetric.Tokens,
            defaultValueDescription: CliParsing.GetEnumToken(AnalysisMetric.Tokens),
            allowedValues: CliParsing.GetEnumTokens(Enum.GetValues<AnalysisMetric>()));
        var surfaces = new CliValueOption<IReadOnlyList<CaptureSurface>>(
            "--surface",
            "SURFACES",
            "Choose which app surfaces to render. Comma-separated values are allowed.",
            CliParsing.ParseCaptureSurfaces,
            defaultFactory: () => defaultSurfaces,
            defaultValueDescription: DescribeSequence(defaultSurfaces),
            allowedValues: [.. CliParsing.GetEnumTokens(Enum.GetValues<CaptureSurface>()), "all"]);
        var palettes = new CliValueOption<IReadOnlyList<TreemapPalette>>(
            "--palette",
            "PALETTES",
            "Choose which treemap palettes to render. Comma-separated values are allowed.",
            CliParsing.ParsePalettes,
            defaultFactory: () => defaultPalettes,
            defaultValueDescription: DescribeSequence(defaultPalettes),
            allowedValues: [.. CliParsing.GetEnumTokens(Enum.GetValues<TreemapPalette>()), "all"]);
        var windowWidth = new CliValueOption<int>(
            "--window-width",
            "N",
            "Override the main-window capture width in pixels.",
            CliParsing.ParseInt,
            defaultFactory: () => 1600,
            defaultValueDescription: "1600");
        var windowHeight = new CliValueOption<int>(
            "--window-height",
            "N",
            "Override the main-window capture height in pixels.",
            CliParsing.ParseInt,
            defaultFactory: () => 1000,
            defaultValueDescription: "1000");
        var treemapWidth = new CliValueOption<int>(
            "--treemap-width",
            "N",
            "Override the standalone treemap capture width in pixels.",
            CliParsing.ParseInt,
            defaultFactory: () => 1320,
            defaultValueDescription: "1320");
        var treemapHeight = new CliValueOption<int>(
            "--treemap-height",
            "N",
            "Override the standalone treemap capture height in pixels.",
            CliParsing.ParseInt,
            defaultFactory: () => 820,
            defaultValueDescription: "820");
        var shareTokens = new CliValueOption<long>(
            "--share-tokens",
            "N",
            "Override the token count shown on the share card without changing the analyzed snapshot.",
            CliParsing.ParseLong);
        var shareLines = new CliValueOption<int>(
            "--share-lines",
            "N",
            "Override the line count shown on the share card without changing the analyzed snapshot.",
            CliParsing.ParseInt);
        var shareFiles = new CliValueOption<int>(
            "--share-files",
            "N",
            "Override the file count shown on the share card without changing the analyzed snapshot.",
            CliParsing.ParseInt);

        var options = new List<CliOption>
        {
            source,
            projectRoot,
            outputDirectory,
            theme,
            metric,
            surfaces,
            palettes,
        };

        CliFlagOption? compare = null;
        if (includeCompareFlag)
        {
            compare = new CliFlagOption(
                "--compare",
                "Generate palette comparison images when multiple palettes are captured.");
            options.Add(compare);
        }

        CliFlagOption? skipCompare = null;
        if (includeSkipCompareFlag)
        {
            skipCompare = new CliFlagOption(
                "--skip-compare",
                "Disable the default palette comparison generation.");
            options.Add(skipCompare);
        }

        options.Add(windowWidth);
        options.Add(windowHeight);
        options.Add(treemapWidth);
        options.Add(treemapHeight);
        options.Add(shareTokens);
        options.Add(shareLines);
        options.Add(shareFiles);

        var command = new CliCommandSpec(name, summary, options, examples);
        return new CaptureCommandDefinition(
            command,
            source,
            projectRoot,
            outputDirectory,
            theme,
            metric,
            palettes,
            surfaces,
            compare,
            skipCompare,
            windowWidth,
            windowHeight,
            treemapWidth,
            treemapHeight,
            shareTokens,
            shareLines,
            shareFiles,
            generateComparisonsByDefault);
    }

    private static CompareCommandDefinition CreateCompareDefinition()
    {
        var left = new CliValueOption<string>(
            "--left",
            "FILE",
            "Point to the first input image.",
            Path.GetFullPath,
            isRequired: true);
        var right = new CliValueOption<string>(
            "--right",
            "FILE",
            "Point to the second input image.",
            Path.GetFullPath,
            isRequired: true);
        var outputDirectory = new CliValueOption<string>(
            "--output-dir",
            "DIR",
            "Override the artifact directory for the diff image and report.",
            Path.GetFullPath,
            defaultFactory: () => CliParsing.GetDefaultArtifactDirectory("visual-compare"),
            defaultValueDescription: ".artifacts/visual-compare/<utc-timestamp>");
        var command = new CliCommandSpec(
            "compare",
            "Produce a diff PNG and JSON report for two existing images.",
            [left, right, outputDirectory],
            [
                $"dotnet run --project {ProjectPath} -- compare --left .artifacts\\visual-harness\\a.png --right .artifacts\\visual-harness\\b.png",
            ]);

        return new CompareCommandDefinition(command, left, right, outputDirectory);
    }

    private static string BuildUsage(CliCommandSpec command)
    {
        var segments = new List<string>
        {
            $"dotnet run --project {ProjectPath} -- {command.Name}",
        };

        foreach (var option in command.Options.Where(option => option.IsRequired))
        {
            segments.Add(option.UsageToken);
        }

        foreach (var option in command.Options.Where(option => !option.IsRequired))
        {
            segments.Add($"[{option.UsageToken}]");
        }

        return string.Join(" ", segments);
    }

    private static string DescribeSequence<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum =>
        string.Join(",", values.Select(CliParsing.GetEnumToken));
}

internal sealed record CliCommandSpec(
    string Name,
    string Summary,
    IReadOnlyList<CliOption> Options,
    IReadOnlyList<string> Examples);

internal abstract class CliOption(
    string name,
    string? valueName,
    string description,
    bool isRequired,
    string? defaultValueDescription,
    IReadOnlyList<string>? allowedValues)
{
    public string Name { get; } = name;

    public string? ValueName { get; } = valueName;

    public string Description { get; } = description;

    public bool IsRequired { get; } = isRequired;

    public string? DefaultValueDescription { get; } = defaultValueDescription;

    public IReadOnlyList<string> AllowedValues { get; } = allowedValues ?? [];

    public abstract bool ExpectsValue { get; }

    public string UsageToken => ExpectsValue
        ? $"{Name} {ValueName}"
        : Name;

    public string GetHelpMetadata()
    {
        var parts = new List<string>();
        if (IsRequired)
        {
            parts.Add("Required");
        }

        if (AllowedValues.Count > 0)
        {
            parts.Add($"Allowed: {string.Join(", ", AllowedValues)}");
        }

        if (!string.IsNullOrWhiteSpace(DefaultValueDescription))
        {
            parts.Add($"Default: {DefaultValueDescription}");
        }

        return string.Join(". ", parts);
    }
}

internal sealed class CliValueOption<T>(
    string name,
    string valueName,
    string description,
    Func<string, T> parser,
    bool isRequired = false,
    Func<T>? defaultFactory = null,
    string? defaultValueDescription = null,
    IReadOnlyList<string>? allowedValues = null)
    : CliOption(name, valueName, description, isRequired, defaultValueDescription, allowedValues)
{
    private readonly Func<string, T> _parser = parser;
    private readonly Func<T>? _defaultFactory = defaultFactory;

    public override bool ExpectsValue => true;

    public T GetValue(string[] args)
    {
        if (CliParsing.GetOptionValue(args, Name) is { } rawValue)
        {
            return _parser(rawValue);
        }

        if (_defaultFactory is not null)
        {
            return _defaultFactory();
        }

        throw new InvalidOperationException($"Missing required option: {Name}");
    }
}

internal sealed class CliFlagOption(string name, string description)
    : CliOption(name, valueName: null, description, isRequired: false, defaultValueDescription: null, allowedValues: null)
{
    public override bool ExpectsValue => false;

    public bool IsSet(string[] args) => CliParsing.HasFlag(args, Name);
}

internal sealed record CaptureCommandDefinition(
    CliCommandSpec Command,
    CliValueOption<CaptureSource> SourceOption,
    CliValueOption<string> ProjectRootOption,
    CliValueOption<string> OutputDirectoryOption,
    CliValueOption<ThemePreference> ThemeOption,
    CliValueOption<AnalysisMetric> MetricOption,
    CliValueOption<IReadOnlyList<TreemapPalette>> PaletteOption,
    CliValueOption<IReadOnlyList<CaptureSurface>> SurfaceOption,
    CliFlagOption? CompareOption,
    CliFlagOption? SkipCompareOption,
    CliValueOption<int> WindowWidthOption,
    CliValueOption<int> WindowHeightOption,
    CliValueOption<int> TreemapWidthOption,
    CliValueOption<int> TreemapHeightOption,
    CliValueOption<long> ShareTokensOption,
    CliValueOption<int> ShareLinesOption,
    CliValueOption<int> ShareFilesOption,
    bool GenerateComparisonsByDefault);

internal sealed record CompareCommandDefinition(
    CliCommandSpec Command,
    CliValueOption<string> LeftOption,
    CliValueOption<string> RightOption,
    CliValueOption<string> OutputDirectoryOption);
