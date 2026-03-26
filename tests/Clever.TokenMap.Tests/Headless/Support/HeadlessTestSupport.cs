using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System.Collections.Generic;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Tests.Headless.Support;

internal static class HeadlessTestSupport
{
    internal static ProjectSnapshot CreateSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    NonEmptyLines: 11,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "Program.cs",
                        Name = "Program.cs",
                        FullPath = "C:\\Demo\\Program.cs",
                        RelativePath = "Program.cs",
                        Kind = ProjectNodeKind.File,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            NonEmptyLines: 11,
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                    },
                },
            },
        };

    internal static ProjectSnapshot CreateNestedSnapshot() =>
        new()
        {
            RootPath = "C:\\Demo",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = new ProjectNode
            {
                Id = "/",
                Name = "Demo",
                FullPath = "C:\\Demo",
                RelativePath = string.Empty,
                Kind = ProjectNodeKind.Root,
                Metrics = new NodeMetrics(
                    Tokens: 42,
                    NonEmptyLines: 11,
                    FileSizeBytes: 128,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 1),
                Children =
                {
                    new ProjectNode
                    {
                        Id = "src",
                        Name = "src",
                        FullPath = "C:\\Demo\\src",
                        RelativePath = "src",
                        Kind = ProjectNodeKind.Directory,
                        Metrics = new NodeMetrics(
                            Tokens: 42,
                            NonEmptyLines: 11,
                            FileSizeBytes: 128,
                            DescendantFileCount: 1,
                            DescendantDirectoryCount: 0),
                        Children =
                        {
                            new ProjectNode
                            {
                                Id = "src/Program.cs",
                                Name = "Program.cs",
                                FullPath = "C:\\Demo\\src\\Program.cs",
                                RelativePath = "src/Program.cs",
                                Kind = ProjectNodeKind.File,
                                Metrics = new NodeMetrics(
                                    Tokens: 42,
                                    NonEmptyLines: 11,
                                    FileSizeBytes: 128,
                                    DescendantFileCount: 1,
                                    DescendantDirectoryCount: 0),
                            },
                        },
                    },
                },
            },
        };

    internal static ProjectNode CreateRootWithChildren(params (string Name, long FileSizeBytes, int Tokens, int NonEmptyLines)[] children)
    {
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Metrics = new NodeMetrics(
                Tokens: children.Sum(item => item.Tokens),
                NonEmptyLines: children.Sum(item => item.NonEmptyLines),
                FileSizeBytes: children.Sum(item => item.FileSizeBytes),
                DescendantFileCount: children.Length,
                DescendantDirectoryCount: 0),
        };

        foreach (var item in children)
        {
            root.Children.Add(new ProjectNode
            {
                Id = item.Name,
                Name = item.Name,
                FullPath = Path.Combine("C:\\Demo", item.Name),
                RelativePath = item.Name,
                Kind = ProjectNodeKind.File,
                Metrics = new NodeMetrics(
                    Tokens: item.Tokens,
                    NonEmptyLines: item.NonEmptyLines,
                    FileSizeBytes: item.FileSizeBytes,
                    DescendantFileCount: 1,
                    DescendantDirectoryCount: 0),
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
            selectedFolderPath,
            recentFolderPaths,
            pathShellService);

    internal static MainWindowViewModel CreateMainWindowViewModel(
        IProjectAnalyzer projectAnalyzer,
        string? selectedFolderPath = "C:\\Demo",
        IEnumerable<string>? recentFolderPaths = null,
        IPathShellService? pathShellService = null) =>
        new(
            new AnalysisSessionController(
                projectAnalyzer,
                new StubFolderPickerService(selectedFolderPath),
                new StubFolderPathService()),
            new TreemapNavigationState(),
            new StubSettingsCoordinator(recentFolderPaths),
            new StubFolderPathService(),
            pathShellService ?? new StubPathShellService());

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
        public SettingsState State { get; } = CreateState(recentFolderPaths);

        public CurrentFolderSettingsState CurrentFolderState { get; } = new();

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

        public void SetSelectedMetric(AnalysisMetric metric) => State.SelectedMetric = metric;

        public void SetRespectGitIgnore(bool value) => State.RespectGitIgnore = value;

        public void SetUseGlobalExcludes(bool value) => State.UseGlobalExcludes = value;

        public void ReplaceGlobalExcludes(IEnumerable<string> entries) => State.ReplaceGlobalExcludes(entries);

        public void SetThemePreference(ThemePreference preference) => State.SelectedThemePreference = preference;

        public void SetTreemapPalette(TreemapPalette palette) => State.SelectedTreemapPalette = palette;

        public void RecordRecentFolder(string folderPath) => State.RecordRecentFolder(folderPath);

        public void RemoveRecentFolder(string folderPath) => State.RemoveRecentFolder(folderPath);

        public void ClearRecentFolders() => State.ClearRecentFolders();

        public void SetUseFolderExcludes(bool value) => CurrentFolderState.UseFolderExcludes = value;

        public void ReplaceFolderExcludes(IEnumerable<string> entries) => CurrentFolderState.ReplaceFolderExcludes(entries);

        public void SwitchActiveFolder(string? rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                CurrentFolderState.Reset();
                return;
            }

            CurrentFolderState.Load(rootPath, useFolderExcludes: false, folderExcludes: []);
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
