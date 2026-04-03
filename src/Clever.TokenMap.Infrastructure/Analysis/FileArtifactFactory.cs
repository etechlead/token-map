namespace Clever.TokenMap.Infrastructure.Analysis;

internal delegate ValueTask<object?> FileArtifactFactory(CancellationToken cancellationToken);
