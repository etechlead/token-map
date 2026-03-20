using System.Collections.Concurrent;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Microsoft.ML.Tokenizers;

namespace Clever.TokenMap.Infrastructure.Tokenization;

public sealed class MicrosoftMlTokenCounter : ITokenCounter
{
    private readonly ConcurrentDictionary<TokenProfile, Tokenizer> _tokenizers = new();

    public ValueTask<int> CountTokensAsync(string content, TokenProfile tokenProfile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var tokenizer = _tokenizers.GetOrAdd(tokenProfile, CreateTokenizer);
        var tokenCount = tokenizer.CountTokens(content, true, true);

        return ValueTask.FromResult(tokenCount);
    }

    private static Tokenizer CreateTokenizer(TokenProfile tokenProfile) =>
        TiktokenTokenizer.CreateForEncoding(GetEncodingName(tokenProfile));

    private static string GetEncodingName(TokenProfile tokenProfile) =>
        tokenProfile switch
        {
            TokenProfile.O200KBase => "o200k_base",
            TokenProfile.Cl100KBase => "cl100k_base",
            TokenProfile.P50KBase => "p50k_base",
            _ => throw new ArgumentOutOfRangeException(nameof(tokenProfile), tokenProfile, "Unsupported token profile."),
        };
}
