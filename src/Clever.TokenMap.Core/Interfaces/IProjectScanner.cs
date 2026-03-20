using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface IProjectScanner
{
    Task<ProjectSnapshot> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken);
}
