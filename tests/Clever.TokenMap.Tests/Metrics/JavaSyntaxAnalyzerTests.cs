using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.Java;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class JavaSyntaxAnalyzerTests
{
    private readonly JavaSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            // lead comment
            class Box {
                /*
                 * block comment
                 */
                int run() {
                    return 0; // trailing
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CommentLineCount);
        Assert.Equal(5, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            class Box {
                Box() {}

                int run(int a) {
                    Runnable nested = () -> {
                        if (a > 0 && a < 10) {
                            return;
                        }
                    };

                    if (a > 0) {
                        for (int i = 0; i < a; i++) {
                            try {
                                if (i > 0) {
                                    return i;
                                } else if (i < 0) {
                                    return a;
                                }
                            } catch (RuntimeException ex) {
                                return 0;
                            }
                        }
                    }

                    return switch (a) {
                        case 1 -> a;
                        default -> 0;
                    };
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(3, summary.FunctionCount);
        Assert.Equal(11, summary.CyclomaticComplexitySum);
        Assert.Equal(7, summary.CyclomaticComplexityMax);
        Assert.Equal(3, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Constructor, callable.Kind);
                Assert.Equal("Box", callable.Name);
                Assert.Equal(1, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(0, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal("run", callable.Name);
                Assert.Equal(7, callable.CyclomaticComplexity);
                Assert.Equal(3, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(0, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSingleAndMultipleLambdaParameters()
    {
        const string sourceText = """
            class Box {
                int run() {
                    java.util.function.Function<Integer, Integer> a = x -> x > 0 ? x : 0;
                    java.util.function.BiFunction<Integer, Integer, Integer> b = (x, y) -> x;
                    return 0;
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal(0, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Equal(1, callable.ParameterCount);
                Assert.Equal(2, callable.CyclomaticComplexity);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Equal(2, callable.ParameterCount);
                Assert.Equal(1, callable.CyclomaticComplexity);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotLetNestedLambdaComplexityLeakIntoParentCallable()
    {
        const string sourceText = """
            class Box {
                int run() {
                    Runnable nested = () -> {
                        if (true) {
                            return;
                        }
                    };

                    return 1;
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        Assert.Equal(2, summary.FunctionCount);

        var callables = summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based).ToArray();
        Assert.Equal(1, callables[0].CyclomaticComplexity);
        Assert.Equal(0, callables[0].MaxNestingDepth);
        Assert.Equal(2, callables[1].CyclomaticComplexity);
        Assert.Equal(1, callables[1].MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_TreatsElseIfChainAsSingleNestingLevel()
    {
        const string sourceText = """
            class Box {
                int run(int x) {
                    if (x == 0) {
                        return 0;
                    } else if (x == 1) {
                        return 1;
                    } else if (x == 2) {
                        return 2;
                    }

                    return 3;
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(4, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsGuardedSwitchRuleButNotDefaultRule()
    {
        const string sourceText = """
            class Box {
                int run(Object value) {
                    return switch (value) {
                        case Integer i when i > 0 -> i;
                        default -> 0;
                    };
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.java", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(3, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            class Box {
                int run(int x {
                    return x;
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.java", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }
}
