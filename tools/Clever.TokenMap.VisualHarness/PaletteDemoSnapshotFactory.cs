using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.VisualHarness;

internal static class PaletteDemoSnapshotFactory
{
    public static ProjectSnapshot Create()
    {
        var rootSpec = new DirectorySpec(
            "TokenMap.Demo",
            [
                new DirectorySpec(
                    "src",
                    [
                        new DirectorySpec(
                            "Clever.TokenMap.App",
                            [
                                new DirectorySpec(
                                    "Assets",
                                    [
                                        new FileSpec("tokenmap-app-icon.ico", 120, 16, 820_000),
                                        new FileSpec("editorconfig.svg", 110, 24, 146_000),
                                        new FileSpec("tokenmap-app-icon.svg", 160, 32, 98_000),
                                        new FileSpec("palette.json", 50, 8, 9_000),
                                    ]),
                                new DirectorySpec(
                                    "Views",
                                    [
                                        new FileSpec("MainWindow.axaml", 980, 220, 24_000),
                                        new FileSpec("SettingsDrawerView.axaml", 640, 170, 16_500),
                                        new FileSpec("ToolbarSummaryView.axaml", 520, 150, 14_200),
                                        new FileSpec("ProjectTreePaneView.axaml", 430, 120, 11_600),
                                    ]),
                                new DirectorySpec(
                                    "ViewModels",
                                    [
                                        new FileSpec("MainWindowViewModel.cs", 1_700, 420, 42_000),
                                        new FileSpec("ToolbarViewModel.cs", 780, 210, 19_000),
                                        new FileSpec("ProjectTreeViewModel.cs", 660, 180, 17_200),
                                    ]),
                                new DirectorySpec(
                                    "State",
                                    [
                                        new FileSpec("TreemapNavigationState.cs", 340, 95, 8_200),
                                    ]),
                                new DirectorySpec(
                                    "Services",
                                    [
                                        new FileSpec("SettingsCoordinator.cs", 900, 250, 20_800),
                                        new FileSpec("AnalysisSessionController.cs", 1_050, 270, 22_400),
                                        new FileSpec("ApplicationThemeService.cs", 210, 60, 4_800),
                                    ]),
                                new FileSpec("App.axaml", 1_300, 260, 27_000),
                            ]),
                        new DirectorySpec(
                            "Clever.TokenMap.Treemap",
                            [
                                new FileSpec("TreemapControl.cs", 2_800, 610, 63_000),
                                new FileSpec("SquarifiedTreemapLayout.cs", 1_250, 280, 30_000),
                                new FileSpec("TreemapColorRules.cs", 600, 150, 12_200),
                                new FileSpec("TreemapVisualRules.cs", 420, 100, 9_000),
                            ]),
                        new DirectorySpec(
                            "Clever.TokenMap.Infrastructure",
                            [
                                new FileSpec("ProjectAnalyzer.cs", 1_100, 250, 26_000),
                                new FileSpec("ProjectSnapshotMetricsEnricher.cs", 760, 180, 18_000),
                                new FileSpec("JsonAppSettingsStore.cs", 1_480, 320, 33_000),
                                new FileSpec("FileSystemProjectScanner.cs", 980, 230, 24_500),
                            ]),
                    ]),
                new DirectorySpec(
                    "tests",
                    [
                        new DirectorySpec(
                            "Clever.TokenMap.Tests",
                            [
                                new FileSpec("ArchitectureRulesTests.cs", 700, 180, 15_000),
                                new FileSpec("MainWindowLayoutTests.cs", 2_400, 520, 56_000),
                                new FileSpec("TreemapControlHeadlessTests.cs", 1_500, 340, 31_000),
                                new FileSpec("SettingsCoordinatorTests.cs", 720, 170, 15_500),
                                new FileSpec("HeadlessTestSupport.cs", 680, 150, 14_200),
                                new FileSpec("ProjectAnalyzerTests.cs", 1_020, 240, 22_000),
                                new FileSpec("TreemapColorRulesTests.cs", 540, 130, 11_400),
                                new FileSpec("SquarifiedTreemapLayoutTests.cs", 860, 200, 18_000),
                            ]),
                    ]),
                new DirectorySpec(
                    "docs",
                    [
                        new FileSpec("architecture.md", 700, 180, 14_000),
                        new FileSpec("workflow.md", 410, 120, 9_500),
                        new FileSpec("AGENTS.md", 380, 100, 7_600),
                    ]),
                new FileSpec("README.md", 320, 90, 8_400),
                new FileSpec("Clever.TokenMap.sln", 260, 70, 11_000),
            ]);

        var rootNode = BuildDirectoryNode(rootSpec, "C:\\VisualHarness", string.Empty, isRoot: true);
        return new ProjectSnapshot
        {
            RootPath = "C:\\VisualHarness",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = rootNode,
        };
    }

    private static ProjectNode BuildDirectoryNode(DirectorySpec directory, string parentFullPath, string parentRelativePath, bool isRoot = false)
    {
        var fullPath = isRoot ? parentFullPath : Path.Combine(parentFullPath, directory.Name);
        var relativePath = isRoot ? string.Empty : AppendPath(parentRelativePath, directory.Name);
        var children = new List<ProjectNode>(directory.Children.Count);

        foreach (var child in directory.Children)
        {
            children.Add(child switch
            {
                FileSpec file => BuildFileNode(file, fullPath, relativePath),
                DirectorySpec subdirectory => BuildDirectoryNode(subdirectory, fullPath, relativePath),
                _ => throw new InvalidOperationException($"Unsupported node spec type: {child.GetType().Name}"),
            });
        }

        var metrics = new NodeMetrics(
            Tokens: children.Sum(item => item.Metrics.Tokens),
            NonEmptyLines: children.Sum(item => item.Metrics.NonEmptyLines),
            FileSizeBytes: children.Sum(item => item.Metrics.FileSizeBytes),
            DescendantFileCount: children.Sum(item => item.Kind == ProjectNodeKind.File ? 1 : item.Metrics.DescendantFileCount),
            DescendantDirectoryCount: children.Sum(item => item.Kind == ProjectNodeKind.Directory ? item.Metrics.DescendantDirectoryCount + 1 : 0));

        var node = new ProjectNode
        {
            Id = isRoot ? "/" : relativePath,
            Name = isRoot ? "VisualHarness" : directory.Name,
            FullPath = fullPath,
            RelativePath = relativePath,
            Kind = isRoot ? ProjectNodeKind.Root : ProjectNodeKind.Directory,
            Metrics = metrics,
        };
        node.Children.AddRange(children);
        return node;
    }

    private static ProjectNode BuildFileNode(FileSpec file, string parentFullPath, string parentRelativePath)
    {
        var relativePath = AppendPath(parentRelativePath, file.Name);
        return new ProjectNode
        {
            Id = relativePath,
            Name = file.Name,
            FullPath = Path.Combine(parentFullPath, file.Name),
            RelativePath = relativePath,
            Kind = ProjectNodeKind.File,
            Metrics = new NodeMetrics(
                Tokens: file.Tokens,
                NonEmptyLines: file.NonEmptyLines,
                FileSizeBytes: file.FileSizeBytes,
                DescendantFileCount: 1,
                DescendantDirectoryCount: 0),
        };
    }

    private static string AppendPath(string basePath, string segment) =>
        string.IsNullOrWhiteSpace(basePath) ? segment : $"{basePath}/{segment}";

    private abstract record NodeSpec(string Name);

    private sealed record FileSpec(string Name, long Tokens, int NonEmptyLines, long FileSizeBytes) : NodeSpec(Name);

    private sealed record DirectorySpec(string Name, IReadOnlyList<NodeSpec> Children) : NodeSpec(Name);
}
