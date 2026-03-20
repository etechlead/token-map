using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface IProjectAnalyzer
{
    Task<ProjectSnapshot> AnalyzeAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken);
}
