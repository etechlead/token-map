using Avalonia.Controls;
using Clever.TokenMap.App.ViewModels;
using Clever.TokenMap.Treemap;

namespace Clever.TokenMap.App.Views.Sections;

public partial class TreemapPaneView : UserControl
{
    public TreemapPaneView()
    {
        InitializeComponent();

        var treemap = this.FindControl<TreemapControl>("ProjectTreemapControl");
        if (treemap is not null)
        {
            treemap.DrillDownRequested += ProjectTreemapControl_OnDrillDownRequested;
        }
    }

    private void ProjectTreemapControl_OnDrillDownRequested(object? sender, TreemapDrillDownRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.DrillIntoTreemap(e.Node);
    }
}

