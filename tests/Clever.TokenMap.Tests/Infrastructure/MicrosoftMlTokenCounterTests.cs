using Clever.TokenMap.Infrastructure.Tokenization;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class MicrosoftMlTokenCounterTests : IDisposable
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

    [Fact]
    public async Task CountTokensAsync_SupportsConcurrentCalls()
    {
        const string content = "const message = 'parallel token counting';\n";

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => _counter.CountTokensAsync(content, CancellationToken.None).AsTask())
            .ToArray();

        var counts = await Task.WhenAll(tasks);

        Assert.All(counts, count => Assert.Equal(counts[0], count));
    }

    public void Dispose()
    {
        _counter.Dispose();
    }
}
