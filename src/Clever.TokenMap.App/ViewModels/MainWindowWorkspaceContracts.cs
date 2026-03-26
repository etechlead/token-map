using System;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.ViewModels;

public interface IToolbarAvailabilitySink
{
    void RefreshAvailability(bool isBusy, bool hasSnapshot);
}

public interface ISummaryProjection
{
    void SetState(AnalysisState state);

    void SetCompleted(ProjectSnapshot snapshot);

    void UpdateProgress(AnalysisProgress progress);
}

public interface IProjectTreeWorkspaceView
{
    event EventHandler<ProjectNode?>? SelectedNodeChanged;

    void LoadRoot(ProjectNode rootNode);

    void Clear();

    void SelectNodeById(string? nodeId);

    void SetShareMetric(AnalysisMetric metric);
}
