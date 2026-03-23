using Clever.TokenMap.Core.Interfaces;
using Microsoft.ML.Tokenizers;

namespace Clever.TokenMap.Infrastructure.Tokenization;

public sealed class MicrosoftMlTokenCounter : ITokenCounter
{
    private const string EncodingName = "o200k_base";
    private readonly Lazy<Tokenizer> _tokenizer = new(() => TiktokenTokenizer.CreateForEncoding(EncodingName));

    public ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var tokenizer = _tokenizer.Value;
        var tokenCount = tokenizer.CountTokens(content, true, true);

        return ValueTask.FromResult(tokenCount);
    }
}
