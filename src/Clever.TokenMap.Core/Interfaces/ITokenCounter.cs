using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Interfaces;

public interface ITokenCounter
{
    ValueTask<int> CountTokensAsync(string content, TokenProfile tokenProfile, CancellationToken cancellationToken);
}
