using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Clever.TokenMap.App.State;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

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
                    TotalLines: 12,
                    NonEmptyLines: 11,
                    BlankLines: 1,
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
                            TotalLines: 12,
                            NonEmptyLines: 11,
                            BlankLines: 1,
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
                    TotalLines: 12,
                    NonEmptyLines: 11,
                    BlankLines: 1,
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
                            TotalLines: 12,
                            NonEmptyLines: 11,
                            BlankLines: 1,
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
                                    TotalLines: 12,
                                    NonEmptyLines: 11,
                                    BlankLines: 1,
                                    FileSizeBytes: 128,
                                    DescendantFileCount: 1,
                                    DescendantDirectoryCount: 0),
                            },
                        },
                    },
                },
            },
        };

    internal static MainWindowViewModel CreateMainWindowViewModel(
        IProjectAnalyzer projectAnalyzer,
        string? selectedFolderPath = "C:\\Demo") =>
        new(
            new AnalysisSessionController(
                projectAnalyzer,
                new StubFolderPickerService(selectedFolderPath)),
            new TreemapNavigationState(),
            new StubSettingsCoordinator());

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

    private sealed class StubSettingsCoordinator : ISettingsCoordinator
    {
        public SettingsState State { get; } = new();
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
