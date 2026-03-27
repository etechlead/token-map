using Clever.TokenMap.Core.Interfaces;
using Microsoft.ML.Tokenizers;

namespace Clever.TokenMap.Infrastructure.Tokenization;

public sealed class MicrosoftMlTokenCounter : ITokenCounter, IDisposable
{
    private const string EncodingName = "o200k_base";
    private readonly ThreadLocal<Tokenizer> _tokenizer =
        new(() => TiktokenTokenizer.CreateForEncoding(EncodingName), trackAllValues: false);

    public ValueTask<int> CountTokensAsync(string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var tokenizer = _tokenizer.Value ?? throw new InvalidOperationException("Tokenizer was not initialized.");
        var tokenCount = tokenizer.CountTokens(content, true, true);

        return ValueTask.FromResult(tokenCount);
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
    }
}
