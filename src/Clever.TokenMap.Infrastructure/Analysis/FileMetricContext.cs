using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Metrics;

namespace Clever.TokenMap.Infrastructure.Analysis;

internal sealed class FileMetricContext : IFileMetricContext
{
    private readonly IReadOnlyDictionary<Type, object?> _artifacts;
    private readonly Func<CancellationToken, ValueTask<FileTextArtifact?>>? _loadTextAsync;
    private FileTextArtifact? _cachedTextArtifact;
    private bool _hasLoadedTextArtifact;

    public FileMetricContext(
        ProjectSnapshot snapshot,
        ProjectNode node,
        long fileSizeBytes,
        IReadOnlyDictionary<Type, object?>? artifacts = null,
        Func<CancellationToken, ValueTask<FileTextArtifact?>>? loadTextAsync = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        FileSizeBytes = fileSizeBytes;
        _artifacts = artifacts ?? new Dictionary<Type, object?>();
        _loadTextAsync = loadTextAsync;
    }

    public ProjectSnapshot Snapshot { get; }

    public ProjectNode Node { get; }

    public string RootPath => Snapshot.RootPath;

    public string FullPath => Node.FullPath;

    public string RelativePath => Node.RelativePath;

    public long FileSizeBytes { get; }

    public async ValueTask<FileTextArtifact?> GetTextAsync(CancellationToken cancellationToken)
    {
        if (_hasLoadedTextArtifact)
        {
            return _cachedTextArtifact;
        }

        if (_loadTextAsync is null)
        {
            _hasLoadedTextArtifact = true;
            return null;
        }

        _cachedTextArtifact = await _loadTextAsync(cancellationToken).ConfigureAwait(false);
        _hasLoadedTextArtifact = true;
        return _cachedTextArtifact;
    }

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
