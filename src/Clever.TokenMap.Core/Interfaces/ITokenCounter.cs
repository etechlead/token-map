namespace Clever.TokenMap.Core.Interfaces;

public interface ITokenCounter
{
    ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken);
}
