using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Infrastructure.Tokenization;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class MicrosoftMlTokenCounterTests
{
    private readonly MicrosoftMlTokenCounter _counter = new();

    [Theory]
    [InlineData(TokenProfile.O200KBase)]
    [InlineData(TokenProfile.Cl100KBase)]
    [InlineData(TokenProfile.P50KBase)]
    public async Task CountTokensAsync_SupportsConfiguredProfiles(TokenProfile tokenProfile)
    {
        const string content = "function sum(a, b) {\n  return a + b;\n}\n";

        var first = await _counter.CountTokensAsync(content, tokenProfile, CancellationToken.None);
        var second = await _counter.CountTokensAsync(content, tokenProfile, CancellationToken.None);

        Assert.True(first > 0);
        Assert.Equal(first, second);
    }
}
