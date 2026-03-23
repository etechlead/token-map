using Clever.TokenMap.Infrastructure.Tokenization;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class MicrosoftMlTokenCounterTests
{
    private readonly MicrosoftMlTokenCounter _counter = new();

    [Fact]
    public async Task CountTokensAsync_UsesSingleHardcodedTokenizer()
    {
        const string content = "function sum(a, b) {\n  return a + b;\n}\n";

        var first = await _counter.CountTokensAsync(content, CancellationToken.None);
        var second = await _counter.CountTokensAsync(content, CancellationToken.None);

        Assert.True(first > 0);
        Assert.Equal(first, second);
    }
}
