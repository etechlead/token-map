using System.Text;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Metrics;

public interface IFileMetricContext
{
    ProjectSnapshot Snapshot { get; }

    ProjectNode Node { get; }

    string RootPath { get; }

    string FullPath { get; }

    string RelativePath { get; }

    long FileSizeBytes { get; }

    ValueTask<FileTextArtifact?> GetTextAsync(CancellationToken cancellationToken);

    ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
        where TArtifact : class;
}

public sealed record FileTextArtifact(
    string Content,
    Encoding Encoding,
    string NewLine);
