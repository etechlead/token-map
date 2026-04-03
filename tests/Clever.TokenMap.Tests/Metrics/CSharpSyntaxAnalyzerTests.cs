using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.CSharp;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class CSharpSyntaxAnalyzerTests
{
    private readonly CSharpSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            // lead comment
            class C
            {
                /*
                 * block comment
                 */
                int M()
                {
                    return 0; // trailing
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.cs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CommentLineCount);
        Assert.Equal(7, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            using System;

            class C
            {
                C() {}

                int M(int x)
                {
                    int Local(int y)
                    {
                        if (y > 0 && y < 10)
                        {
                            return y;
                        }

                        return 0;
                    }

                    Func<int, int> a = z => z > 0 ? z : 0;
                    Func<int, int> b = delegate(int z) { return z; };

                    try
                    {
                        switch (x)
                        {
                            case > 0 when x < 10:
                                return x;
                            case 0:
                                return 0;
                            default:
                                return -1;
                        }
                    }
                    catch (Exception) when (x > 1)
                    {
                        return -2;
                    }
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.cs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(5, summary.FunctionCount);
        Assert.Equal(13, summary.CyclomaticComplexitySum);
        Assert.Equal(6, summary.CyclomaticComplexityMax);
        Assert.Equal(1, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Constructor, callable.Kind);
                Assert.Equal("C", callable.Name);
                Assert.Equal(1, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(0, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal("M", callable.Name);
                Assert.Equal(6, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.LocalFunction, callable.Kind);
                Assert.Equal("Local", callable.Name);
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Closure, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(1, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            class C
            {
                int M(
                {
                    return 0;
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.cs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }
}
