using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Preview;
using Clever.TokenMap.Core.Settings;
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

    [Fact]
    public async Task PreviewNodeAsync_BuildsExplainabilitySectionsFromComputedMetrics()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new PreviewReaderByPath([
                new KeyValuePair<string, FilePreviewContentResult>(file.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview"))
            ]));

        await viewModel.PreviewNodeAsync(file);

        var explainability = Assert.IsType<FilePreviewExplainabilityViewModel>(viewModel.FilePreviewExplainability);
        Assert.Equal(
            ["Structural Risk", "Refactor Priority"],
            explainability.Sections.Select(section => section.Title).ToArray());
        Assert.Contains(
            explainability.Sections[0].Contributors,
            contributor => contributor.Label == "Total callable burden");
        Assert.True(explainability.Sections[1].HasContributors);
        Assert.False(explainability.Sections[1].HasNote);
        Assert.Contains("amplified by recent change pressure", explainability.Sections[1].Summary);
    }

    [Fact]
    public async Task PreviewNodeAsync_NotesWhenRefactorPriorityLacksGitContext()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: false);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            filePreviewContentReader: new PreviewReaderByPath([
                new KeyValuePair<string, FilePreviewContentResult>(file.FullPath, new FilePreviewContentResult(FilePreviewReadStatus.Success, "preview"))
            ]));

        await viewModel.PreviewNodeAsync(file);

        var explainability = Assert.IsType<FilePreviewExplainabilityViewModel>(viewModel.FilePreviewExplainability);
        Assert.True(explainability.Sections[1].HasNote);
    }

    [Fact]
    public void OpenRefactorPrompt_CreatesEditablePromptForFileNode()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));

        viewModel.OpenRefactorPrompt(file);

        var refactorPrompt = Assert.IsType<RefactorPromptViewModel>(viewModel.RefactorPrompt);
        Assert.True(viewModel.IsRefactorPromptOpen);
        Assert.Equal("Program.cs", refactorPrompt.RelativePath);
        Assert.Contains("Relative path: Program.cs", refactorPrompt.PromptText);
        Assert.Contains("Observed metrics:", refactorPrompt.PromptText);
        Assert.Contains("Total callable burden", refactorPrompt.PromptText);
        Assert.Contains("Task:", refactorPrompt.PromptText);
    }

    [Fact]
    public void OpenRefactorPrompt_NotesWhenGitContextIsUnavailable()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: false);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));

        viewModel.OpenRefactorPrompt(file);

        var refactorPrompt = Assert.IsType<RefactorPromptViewModel>(viewModel.RefactorPrompt);
        Assert.Contains("git-derived change and co-change inputs are unavailable", refactorPrompt.PromptText);
    }

    [Fact]
    public void OpenRefactorPrompt_UsesCustomTemplateFromSettings()
    {
        const string customTemplate =
            """
            Candidate:
            {{relative_path}}
            Structural Risk => {{structural_risk}}
            {{structural_risk_breakdown}}
            """;
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(
            new StubProjectAnalyzer(snapshot),
            refactorPromptTemplate: customTemplate);

        viewModel.OpenRefactorPrompt(file);

        var refactorPrompt = Assert.IsType<RefactorPromptViewModel>(viewModel.RefactorPrompt);
        Assert.Contains("Candidate:", refactorPrompt.PromptText);
        Assert.Contains("Program.cs", refactorPrompt.PromptText);
        Assert.Contains("Structural Risk =>", refactorPrompt.PromptText);
        Assert.DoesNotContain("Observed metrics:", refactorPrompt.PromptText);
    }

    [Fact]
    public void SaveRefactorPromptTemplateEditor_AppliesTemplateToFuturePrompts()
    {
        const string customTemplate = "Path={{relative_path}}\nPriority={{refactor_priority}}";
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        var templateSettings = viewModel.RefactorPromptTemplateSettings;

        templateSettings.OpenEditor();
        Assert.NotNull(templateSettings.Editor);
        templateSettings.Editor!.TemplateText = customTemplate;

        templateSettings.SaveEditor();
        viewModel.OpenRefactorPrompt(file);

        var refactorPrompt = Assert.IsType<RefactorPromptViewModel>(viewModel.RefactorPrompt);
        Assert.StartsWith("Path=Program.cs\nPriority=", refactorPrompt.PromptText);
        Assert.DoesNotContain("Observed metrics:", refactorPrompt.PromptText);
        Assert.False(templateSettings.IsEditorOpen);
    }

    [Fact]
    public void CloseRefactorPromptTemplateEditor_DiscardsUnsavedTemplateChanges()
    {
        var snapshot = CreateExplainabilitySnapshot(includeGitContext: true);
        var file = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));
        var templateSettings = viewModel.RefactorPromptTemplateSettings;

        templateSettings.OpenEditor();
        Assert.NotNull(templateSettings.Editor);
        templateSettings.Editor!.TemplateText = "Discard {{relative_path}}";
        templateSettings.CloseEditor();

        viewModel.OpenRefactorPrompt(file);

        var refactorPrompt = Assert.IsType<RefactorPromptViewModel>(viewModel.RefactorPrompt);
        Assert.Contains("Observed metrics:", refactorPrompt.PromptText);
        Assert.DoesNotContain("Discard Program.cs", refactorPrompt.PromptText);
    }

    [Fact]
    public void ResetRefactorPromptTemplateEditorToDefault_RestoresDefaultTemplateText()
    {
        var viewModel = CreateMainWindowViewModel();
        var templateSettings = viewModel.RefactorPromptTemplateSettings;

        templateSettings.OpenEditor();
        Assert.NotNull(templateSettings.Editor);
        templateSettings.Editor!.TemplateText = "Custom";

        templateSettings.ResetEditorCommand.Execute(null);

        Assert.Equal(
            RefactorPromptTemplateDefaults.DefaultRefactorPromptTemplate,
            templateSettings.Editor.TemplateText);
    }

    [Fact]
    public void OpenRefactorPrompt_IgnoresDirectories()
    {
        var snapshot = CreateNestedSnapshot();
        var directory = Assert.Single(snapshot.Root.Children);
        var viewModel = CreateMainWindowViewModel(new StubProjectAnalyzer(snapshot));

        viewModel.OpenRefactorPrompt(directory);

        Assert.False(viewModel.IsRefactorPromptOpen);
        Assert.Null(viewModel.RefactorPrompt);
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
