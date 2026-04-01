using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface IProjectSnapshotMetricEngine
{
    Task<ProjectSnapshot> EnrichAsync(
        ProjectSnapshot snapshot,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken);
}
