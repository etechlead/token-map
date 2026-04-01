using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Infrastructure.Analysis;

internal sealed class FileMetricContext : IFileMetricContext
{
    private readonly IReadOnlyDictionary<Type, object?> _artifacts;

    public FileMetricContext(
        long fileSizeBytes,
        IReadOnlyDictionary<Type, object?>? artifacts = null)
    {
        FileSizeBytes = fileSizeBytes;
        _artifacts = artifacts ?? new Dictionary<Type, object?>();
    }

    public long FileSizeBytes { get; }

    public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
        where TArtifact : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _artifacts.TryGetValue(typeof(TArtifact), out var artifact)
                ? artifact as TArtifact
                : null);
    }
}
