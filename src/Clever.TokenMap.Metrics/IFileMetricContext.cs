namespace Clever.TokenMap.Metrics;

public interface IFileMetricContext
{
    long FileSizeBytes { get; }

    ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
        where TArtifact : class;
}
