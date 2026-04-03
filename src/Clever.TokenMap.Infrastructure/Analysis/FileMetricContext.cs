using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Infrastructure.Analysis;

internal sealed class FileMetricContext : IFileMetricContext
{
    private readonly IReadOnlyDictionary<Type, object?> _artifacts;
    private readonly IReadOnlyDictionary<Type, FileArtifactFactory> _artifactFactories;
    private readonly Dictionary<Type, object?> _resolvedArtifacts = [];

    public FileMetricContext(
        long fileSizeBytes,
        IReadOnlyDictionary<Type, object?>? artifacts = null,
        IReadOnlyDictionary<Type, FileArtifactFactory>? artifactFactories = null)
    {
        FileSizeBytes = fileSizeBytes;
        _artifacts = artifacts ?? new Dictionary<Type, object?>();
        _artifactFactories = artifactFactories ?? new Dictionary<Type, FileArtifactFactory>();
    }

    public long FileSizeBytes { get; }

    public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
        where TArtifact : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetArtifactCoreAsync<TArtifact>(cancellationToken);
    }

    private async ValueTask<TArtifact?> GetArtifactCoreAsync<TArtifact>(CancellationToken cancellationToken)
        where TArtifact : class
    {
        var artifactType = typeof(TArtifact);
        if (_resolvedArtifacts.TryGetValue(artifactType, out var resolvedArtifact))
        {
            return resolvedArtifact as TArtifact;
        }

        if (_artifacts.TryGetValue(artifactType, out var eagerArtifact))
        {
            _resolvedArtifacts[artifactType] = eagerArtifact;
            return eagerArtifact as TArtifact;
        }

        if (!_artifactFactories.TryGetValue(artifactType, out var artifactFactory))
        {
            return null;
        }

        var createdArtifact = await artifactFactory(cancellationToken).ConfigureAwait(false);
        _resolvedArtifacts[artifactType] = createdArtifact;
        return createdArtifact as TArtifact;
    }
}
