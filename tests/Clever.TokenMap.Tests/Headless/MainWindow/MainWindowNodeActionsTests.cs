using System.Threading;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Models;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

using Clever.TokenMap.Tests.Headless.Support;

namespace Clever.TokenMap.Tests.Headless.MainWindow;

public sealed class MainWindowNodeActionsTests
{
    [Fact]
    public async Task MainWindowViewModel_OpenNodeAsync_DelegatesToShellService()
    {
        var snapshot = CreateSnapshot();
        var node = Assert.Single(snapshot.Root.Children);
        var pathShellService = new RecordingPathShellService();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            pathShellService: pathShellService);

        await viewModel.OpenNodeAsync(node);

        Assert.Equal(node.FullPath, pathShellService.OpenedPath);
    }

    [Fact]
    public async Task MainWindowViewModel_RevealNodeAsync_PassesDirectoryFlagForDirectories()
    {
        var snapshot = CreateNestedSnapshot();
        var node = Assert.Single(snapshot.Root.Children);
        var pathShellService = new RecordingPathShellService();
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            pathShellService: pathShellService);

        await viewModel.RevealNodeAsync(node);

        Assert.Equal(node.FullPath, pathShellService.RevealedPath);
        Assert.True(pathShellService.RevealedIsDirectory);
    }

    private sealed class RecordingPathShellService : IPathShellService
    {
        public string RevealMenuHeader => "Reveal";

        public string? OpenedPath { get; private set; }

        public string? RevealedPath { get; private set; }

        public bool RevealedIsDirectory { get; private set; }

        public Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            OpenedPath = fullPath;
            return Task.FromResult(true);
        }

        public Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default)
        {
            RevealedPath = fullPath;
            RevealedIsDirectory = isDirectory;
            return Task.FromResult(true);
        }
    }
}
