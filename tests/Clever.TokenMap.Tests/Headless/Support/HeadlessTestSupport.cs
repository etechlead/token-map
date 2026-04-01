using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Support;

namespace Clever.TokenMap.Tests.Headless.Support;

internal static class HeadlessTestSupport
{
    internal static string GetTestFolderPath(string folderName) => TestPaths.Folder(folderName);

    internal static ProjectSnapshot CreateSnapshot() =>
        new()
        {
            RootPath = TestPaths.Folder("Demo"),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = TestPaths.Folder("Demo"),
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
                ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "Program.cs",
                        Name = "Program.cs",
                        FullPath = TestPaths.CombineUnder(TestPaths.Folder("Demo"), "Program.cs"),
                        RelativePath = "Program.cs",
                        Kind = ProjectNodeKind.File,
                        Summary = MetricTestData.CreateFileSummary(),
                        ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                    },
                },
            },
        };

    internal static ProjectSnapshot CreateNestedSnapshot() =>
        new()
        {
            RootPath = TestPaths.Folder("Demo"),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = TestPaths.Folder("Demo"),
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 1),
                ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "src",
                        Name = "src",
                        FullPath = TestPaths.CombineUnder(TestPaths.Folder("Demo"), "src"),
                        RelativePath = "src",
                        Kind = ProjectNodeKind.Directory,
                        Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 1, descendantDirectoryCount: 0),
                        ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                        Children =
                        {
                            new ProjectNode
                            {
                                Id = "src/Program.cs",
                                Name = "Program.cs",
                                FullPath = TestPaths.CombineUnder(TestPaths.Folder("Demo"), "src", "Program.cs"),
                                RelativePath = "src/Program.cs",
                                Kind = ProjectNodeKind.File,
                                Summary = MetricTestData.CreateFileSummary(),
                                ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 42, nonEmptyLines: 11, fileSizeBytes: 128),
                            },
                        },
                    },
                },
            },
        };

    internal static ProjectNode CreateRootWithChildren(params (string Name, long FileSizeBytes, int Tokens, int NonEmptyLines)[] children)
    {
        var demoRootPath = TestPaths.Folder("Demo");
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = demoRootPath,
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: children.Length, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(
                tokens: children.Sum(item => item.Tokens),
                nonEmptyLines: children.Sum(item => item.NonEmptyLines),
                fileSizeBytes: children.Sum(item => item.FileSizeBytes)),
        };

        foreach (var item in children)
        {
            root.Children.Add(new ProjectNode
            {
                Id = item.Name,
                Name = item.Name,
                FullPath = Path.Combine(demoRootPath, item.Name),
                RelativePath = item.Name,
                Kind = ProjectNodeKind.File,
                Summary = MetricTestData.CreateFileSummary(),
                ComputedMetrics = MetricTestData.CreateComputedMetrics(
                    tokens: item.Tokens,
                    nonEmptyLines: item.NonEmptyLines,
                    fileSizeBytes: item.FileSizeBytes),
            });
        }

        return root;
    }

    internal static MainWindowViewModel CreateMainWindowViewModel(
        string? selectedFolderPath = null,
        IEnumerable<string>? recentFolderPaths = null,
        IPathShellService? pathShellService = null) =>
        CreateMainWindowViewModel(
            new StubProjectAnalyzer(CreateSnapshot()),
            selectedFolderPath ?? TestPaths.Folder("Demo"),
            recentFolderPaths,
            pathShellService);

    internal static MainWindowViewModel CreateMainWindowViewModel(
        IProjectAnalyzer projectAnalyzer,
        string? selectedFolderPath = null,
        IEnumerable<string>? recentFolderPaths = null,
        IPathShellService? pathShellService = null)
    {
        var analysisSessionController = new AnalysisSessionController(
            projectAnalyzer,
            new StubFolderPickerService(selectedFolderPath ?? TestPaths.Folder("Demo")),
            new StubFolderPathService());
        var settingsCoordinator = new StubSettingsCoordinator(recentFolderPaths);
        var folderPathService = new StubFolderPathService();
        var appIssueState = new AppIssueState();

        return MainWindowViewModelFactory.Create(
                new MainWindowViewModelFactoryDependencies(
                    analysisSessionController,
                    settingsCoordinator,
                    folderPathService,
                    pathShellService ?? new StubPathShellService(),
                    new TestAppIssueReporter(appIssueState),
                    appIssueState,
                    new StubAppStoragePaths(),
                    new StubApplicationControlService()))
            .MainWindowViewModel;
    }

    internal static T? FindNamedDescendant<T>(Window window, string name)
        where T : Control
    {
        return window.GetLogicalDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal))
            ?? window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
    }

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult(path);
    }

    private sealed class StubFolderPathService : IFolderPathService
    {
        public bool Exists(string folderPath) => true;
    }

    private sealed class StubSettingsCoordinator(IEnumerable<string>? recentFolderPaths = null) : ISettingsCoordinator
    {
        private SettingsState MutableState { get; } = CreateState(recentFolderPaths);

        private CurrentFolderSettingsState MutableCurrentFolderState { get; } = new();

        public IReadOnlySettingsState State => MutableState;

        public IReadOnlyCurrentFolderSettingsState CurrentFolderState => MutableCurrentFolderState;

        public ScanOptions BuildCurrentScanOptions() =>
            new()
            {
                RespectGitIgnore = State.RespectGitIgnore,
                UseGlobalExcludes = State.UseGlobalExcludes,
                GlobalExcludes = [.. State.GlobalExcludes],
                UseFolderExcludes = CurrentFolderState.UseFolderExcludes,
                FolderExcludes = [.. CurrentFolderState.FolderExcludes],
            };

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ScanOptions Resolve(string? rootPath, ScanOptions baseOptions) => baseOptions;

        public void SetSelectedMetric(MetricId metric) => MutableState.SelectedMetric = DefaultMetricCatalog.NormalizeMetricId(metric);

        public void SetRespectGitIgnore(bool value) => MutableState.RespectGitIgnore = value;

        public void SetUseGlobalExcludes(bool value) => MutableState.UseGlobalExcludes = value;

        public void ReplaceGlobalExcludes(IEnumerable<string> entries) => MutableState.ReplaceGlobalExcludes(entries);

        public void SetThemePreference(ThemePreference preference) => MutableState.SelectedThemePreference = preference;

        public void SetTreemapPalette(TreemapPalette palette) => MutableState.SelectedTreemapPalette = palette;

        public void SetShowTreemapMetricValues(bool value) => MutableState.ShowTreemapMetricValues = value;

        public void RecordRecentFolder(string folderPath) => MutableState.RecordRecentFolder(folderPath);

        public void RemoveRecentFolder(string folderPath) => MutableState.RemoveRecentFolder(folderPath);

        public void ClearRecentFolders() => MutableState.ClearRecentFolders();

        public void SetUseFolderExcludes(bool value) => MutableCurrentFolderState.UseFolderExcludes = value;

        public void ReplaceFolderExcludes(IEnumerable<string> entries) => MutableCurrentFolderState.ReplaceFolderExcludes(entries);

        public void SwitchActiveFolder(string? rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                MutableCurrentFolderState.Reset();
                return;
            }

            MutableCurrentFolderState.Load(rootPath, useFolderExcludes: false, folderExcludes: []);
        }

        private static SettingsState CreateState(IEnumerable<string>? recentFolderPaths)
        {
            var state = new SettingsState();
            if (recentFolderPaths is null)
            {
                return state;
            }

            foreach (var folderPath in recentFolderPaths)
            {
                state.RecordRecentFolder(folderPath);
            }

            return state;
        }
    }

    private sealed class StubPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubApplicationControlService : IApplicationControlService
    {
        public void RequestShutdown(int exitCode = 0)
        {
        }
    }

    private sealed class StubAppStoragePaths : IAppStoragePaths
    {
        public string GetSettingsFilePath() => Path.Combine(TestPaths.Folder("Demo"), "settings.json");

        public string GetFolderSettingsRootPath() => Path.Combine(TestPaths.Folder("Demo"), "folders");

        public string GetLogsDirectoryPath() => Path.Combine(TestPaths.Folder("Demo"), "logs");
    }

    private sealed class TestAppIssueReporter(AppIssueState state) : IAppIssueReporter
    {
        public void Report(AppIssue issue)
        {
            ArgumentNullException.ThrowIfNull(issue);

            state.Show(new DisplayedAppIssue(issue, "TEST-ISSUE", DateTimeOffset.UtcNow));
        }
    }
}

internal sealed class StubProjectAnalyzer(ProjectSnapshot snapshot) : IProjectAnalyzer
{
    public Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(snapshot);
}
