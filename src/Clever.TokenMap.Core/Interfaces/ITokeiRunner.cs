using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Core.Interfaces;

public interface ITokeiRunner
{
    Task<IReadOnlyDictionary<string, TokeiFileStats>> CollectAsync(
        string rootPath,
        IReadOnlyCollection<string> includedRelativePaths,
        CancellationToken cancellationToken);
}
