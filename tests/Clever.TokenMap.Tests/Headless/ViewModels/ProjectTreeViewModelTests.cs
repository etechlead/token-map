using System.ComponentModel;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Tests.Support;
using static Clever.TokenMap.Tests.Headless.Support.HeadlessTestSupport;

namespace Clever.TokenMap.Tests.Headless.ViewModels;

public sealed class ProjectTreeViewModelTests
{
    [Fact]
    public void LoadRoot_DefaultsToTokensDescendingSort()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = CreateRootWithChildren(
            ("Small.cs", 10, 20, 1),
            ("Large.cs", 20, 10, 1));

        viewModel.LoadRoot(root);

        Assert.Equal(ProjectTreeSortColumn.Metric, viewModel.CurrentSortColumn);
        Assert.Equal(MetricIds.Tokens, viewModel.CurrentMetricSortId);
        Assert.Equal(ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Small.cs", name),
            name => Assert.Equal("Large.cs", name));
    }

    [Fact]
    public void LoadRoot_PreservesActiveSortAcrossReloads()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));

        viewModel.SortByMetric(MetricIds.Tokens, ListSortDirection.Ascending);
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Gamma.cs", 30, 30, 1),
            ("Delta.cs", 5, 5, 1)));

        Assert.Equal(ProjectTreeSortColumn.Metric, viewModel.CurrentSortColumn);
        Assert.Equal(MetricIds.Tokens, viewModel.CurrentMetricSortId);
        Assert.Equal(ListSortDirection.Ascending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Delta.cs", name),
            name => Assert.Equal("Gamma.cs", name));
    }

    [Fact]
    public void SortBy_ReordersVisibleRows()
    {
        var viewModel = new ProjectTreeViewModel();
        var root = new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 2, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 30, nonEmptyLines: 3, fileSizeBytes: 30),
            Children =
            {
                new ProjectNode
                {
                    Id = "A.cs",
                    Name = "A.cs",
                    FullPath = "C:\\Demo\\A.cs",
                    RelativePath = "A.cs",
                    Kind = ProjectNodeKind.File,
                    Summary = MetricTestData.CreateFileSummary(),
                    ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 10, nonEmptyLines: 1, fileSizeBytes: 10),
                },
                new ProjectNode
                {
                    Id = "B.cs",
                    Name = "B.cs",
                    FullPath = "C:\\Demo\\B.cs",
                    RelativePath = "B.cs",
                    Kind = ProjectNodeKind.File,
                    Summary = MetricTestData.CreateFileSummary(),
                    ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 20, nonEmptyLines: 2, fileSizeBytes: 20),
                },
            },
        };

        viewModel.LoadRoot(root);
        viewModel.SortByMetric(MetricIds.Tokens, ListSortDirection.Descending);

        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("B.cs", name),
            name => Assert.Equal("A.cs", name));
    }

    [Fact]
    public void SetVisibleMetrics_FallsBackToFirstVisibleMetricSort()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 10, 10, 1),
            ("Beta.cs", 20, 20, 1)));
        viewModel.SortByMetric(MetricIds.FileSizeBytes, ListSortDirection.Descending);

        viewModel.SetVisibleMetrics([MetricIds.NonEmptyLines, MetricIds.FileSizeBytes]);
        Assert.Equal(MetricIds.FileSizeBytes, viewModel.CurrentMetricSortId);

        viewModel.SetVisibleMetrics([MetricIds.NonEmptyLines]);

        Assert.Equal(ProjectTreeSortColumn.Metric, viewModel.CurrentSortColumn);
        Assert.Equal(MetricIds.NonEmptyLines, viewModel.CurrentMetricSortId);
    }

    [Fact]
    public void SortByParentShare_KeepsNaRowsLast()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(new ProjectNode
        {
            Id = "/",
            Name = "Demo",
            FullPath = "C:\\Demo",
            RelativePath = string.Empty,
            Kind = ProjectNodeKind.Root,
            Summary = MetricTestData.CreateDirectorySummary(descendantFileCount: 3, descendantDirectoryCount: 0),
            ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 30, nonEmptyLines: 20, fileSizeBytes: 40),
            Children =
            {
                new ProjectNode
                {
                    Id = "Alpha.cs",
                    Name = "Alpha.cs",
                    FullPath = "C:\\Demo\\Alpha.cs",
                    RelativePath = "Alpha.cs",
                    Kind = ProjectNodeKind.File,
                    Summary = MetricTestData.CreateFileSummary(),
                    ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 20, nonEmptyLines: 10, fileSizeBytes: 10),
                },
                new ProjectNode
                {
                    Id = "Beta.cs",
                    Name = "Beta.cs",
                    FullPath = "C:\\Demo\\Beta.cs",
                    RelativePath = "Beta.cs",
                    Kind = ProjectNodeKind.File,
                    Summary = MetricTestData.CreateFileSummary(),
                    ComputedMetrics = MetricTestData.CreateComputedMetrics(tokens: 10, nonEmptyLines: 10, fileSizeBytes: 20),
                },
                new ProjectNode
                {
                    Id = "binary.dat",
                    Name = "binary.dat",
                    FullPath = "C:\\Demo\\binary.dat",
                    RelativePath = "binary.dat",
                    Kind = ProjectNodeKind.File,
                    SkippedReason = SkippedReason.Binary,
                    Summary = MetricTestData.CreateFileSummary(),
                    ComputedMetrics = MetricTestData.CreateSkippedComputedMetrics(fileSizeBytes: 10),
                },
            },
        });

        viewModel.SortBy(ProjectTreeSortColumn.ParentShare, ListSortDirection.Descending);

        Assert.Equal(ProjectTreeSortColumn.ParentShare, viewModel.CurrentSortColumn);
        Assert.Equal(ListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Collection(
            viewModel.VisibleNodes.Select(node => node.Name),
            name => Assert.Equal("Demo", name),
            name => Assert.Equal("Alpha.cs", name),
            name => Assert.Equal("Beta.cs", name),
            name => Assert.Equal("binary.dat", name));
    }

    [Fact]
    public void ToggleNodeCommand_ShowsAndHidesChildrenInVisibleRows()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);

        var directoryNode = Assert.Single(viewModel.VisibleNodes, node => node.Node.Id == "src");
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.Node.Id == "src/Program.cs");

        viewModel.ToggleNodeCommand.Execute(directoryNode);
        Assert.Contains(viewModel.VisibleNodes, node => node.Node.Id == "src/Program.cs");

        viewModel.ToggleNodeCommand.Execute(directoryNode);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.Node.Id == "src/Program.cs");
    }

    [Fact]
    public void MoveSelectionRight_ExpandsSelectedDirectory()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
        Assert.Contains(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [Fact]
    public void MoveSelectionRight_OnExpandedDirectory_SelectsFirstChild()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");
        viewModel.MoveSelectionRight();

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("src/Program.cs", viewModel.SelectedNode?.Node.Id);
    }

    [Fact]
    public void MoveSelectionLeft_CollapsesExpandedDirectory()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");
        viewModel.MoveSelectionRight();

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
        Assert.DoesNotContain(viewModel.VisibleNodes, node => node.RelativePath == "src/Program.cs");
    }

    [Fact]
    public void MoveSelectionLeft_OnCollapsedDirectory_SelectsParent()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src");

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("/", viewModel.SelectedNode?.Node.Id);
    }

    [Fact]
    public void MoveSelectionLeft_OnFile_SelectsParent()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateNestedSnapshot().Root);
        viewModel.SelectNodeById("src/Program.cs");

        var changed = viewModel.MoveSelectionLeft();

        Assert.True(changed);
        Assert.Equal("src", viewModel.SelectedNode?.Node.Id);
    }

    [Fact]
    public void MoveSelectionRight_OnFile_SelectsNextVisibleNode()
    {
        var viewModel = new ProjectTreeViewModel();
        viewModel.LoadRoot(CreateRootWithChildren(
            ("Alpha.cs", 20, 20, 1),
            ("Beta.cs", 10, 10, 1)));
        viewModel.SelectNodeById("Alpha.cs");

        var changed = viewModel.MoveSelectionRight();

        Assert.True(changed);
        Assert.Equal("Beta.cs", viewModel.SelectedNode?.Node.Id);
    }
}
