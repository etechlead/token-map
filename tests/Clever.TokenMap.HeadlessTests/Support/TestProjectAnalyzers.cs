using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.HeadlessTests;

internal sealed class CountingProjectAnalyzer(params ProjectSnapshot[] snapshots) : IProjectAnalyzer
{
    private readonly Queue<ProjectSnapshot> _snapshots = new(snapshots);

    public int CallCount { get; private set; }

    public Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        CallCount++;
        if (_snapshots.Count == 0)
        {
            throw new InvalidOperationException("No more snapshots configured.");
        }

        return Task.FromResult(_snapshots.Dequeue());
    }
}

internal sealed class CancelAwareProjectAnalyzer : IProjectAnalyzer
{
    public async Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new AnalysisProgress("ScanningTree", 1, 2, "Program.cs"));
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

        throw new InvalidOperationException("This path should have been cancelled.");
    }
}
