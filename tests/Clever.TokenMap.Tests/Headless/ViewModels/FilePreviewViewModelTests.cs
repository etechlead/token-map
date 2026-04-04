using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Preview;
using Clever.TokenMap.Tests.Headless.Support;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class FilePreviewViewModelTests
{
    [Fact]
    public async Task PreviewNodeAsync_OpensPreviewForFileNode()
    {
        var snapshot = CreateTwoFileSnapshot();
        var firstFile = snapshot.Root.Children[0];
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new PreviewReaderByPath([
                new KeyValuePair<string, FilePreviewContentResult>(firstFile.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "first"))
            ]));

        await viewModel.PreviewNodeAsync(firstFile);

        Assert.True(viewModel.IsFilePreviewOpen);
        Assert.Same(firstFile, viewModel.FilePreview.Node);
        Assert.Equal("first", viewModel.FilePreview.Content);
        Assert.True(viewModel.FilePreview.ShowEditor);
    }

    [Fact]
    public async Task PreviewNodeAsync_ReplacesCurrentPreview_WhenAnotherFileIsOpened()
    {
        var snapshot = CreateTwoFileSnapshot();
        var firstFile = snapshot.Root.Children[0];
        var secondFile = snapshot.Root.Children[1];
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new PreviewReaderByPath([
                new KeyValuePair<string, FilePreviewContentResult>(firstFile.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "first")),
                new KeyValuePair<string, FilePreviewContentResult>(secondFile.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "second"))
            ]));

        await viewModel.PreviewNodeAsync(firstFile);
        await viewModel.PreviewNodeAsync(secondFile);

        Assert.True(viewModel.IsFilePreviewOpen);
        Assert.Same(secondFile, viewModel.FilePreview.Node);
        Assert.Equal("second", viewModel.FilePreview.Content);
        Assert.Equal("Beta.cs", viewModel.FilePreview.DisplayName);
    }

    [Fact]
    public async Task CloseFilePreview_ClearsPreviewState()
    {
        var snapshot = CreateSnapshot();
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new PreviewReaderByPath([
                new KeyValuePair<string, FilePreviewContentResult>(file.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview"))
            ]));

        await viewModel.PreviewNodeAsync(file);
        viewModel.CloseFilePreview();

        Assert.False(viewModel.IsFilePreviewOpen);
        Assert.Null(viewModel.FilePreview.Node);
        Assert.Equal(string.Empty, viewModel.FilePreview.Content);
    }

    [Fact]
    public async Task PreviewNodeAsync_IgnoresDirectories()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));

        await viewModel.PreviewNodeAsync(directory);

        Assert.False(viewModel.IsFilePreviewOpen);
    }

    private static ProjectSnapshot CreateTwoFileSnapshot()
    {
        var root = CreateRootWithChildren(
            ("Alpha.cs", 10, 5, 5),
            ("Beta.cs", 20, 7, 7));

        return new ProjectSnapshot
        {
            RootPath = root.FullPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Options = ScanOptions.Default,
            Root = root,
        };
    }

    private sealed class PreviewReaderByPath(
        IEnumerable<KeyValuePair<string, FilePreviewContentResult>> results) : IFilePreviewContentReader
    {
        private readonly Dictionary<string, FilePreviewContentResult> _results = new(results, StringComparer.Ordinal);

        public Task<FilePreviewContentResult> ReadAsync(string fullPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(_results[fullPath]);
    }
}
