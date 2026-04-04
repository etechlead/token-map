using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.Go;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class GoSyntaxAnalyzerTests
{
    private readonly GoSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            // lead comment
            package sample

            /*
             * block comment
             */
            func top() int {
                return 0 // trailing
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            package sample

            type Box struct{}

            func (b *Box) Run(x int) int {
                switch x {
                case 1:
                    return x
                default:
                    return 0
                }
            }

            func top(x int, items []int, ch chan int) int {
                local := func(y int) int {
                    if y > 0 && y < 10 {
                        return y
                    }

                    return 0
                }

                if x > 0 {
                    for _, item := range items {
                        if item > 0 {
                            return item
                        } else if item < 0 {
                            return x
                        }
                    }
                }

                select {
                case value := <-ch:
                    return value
                default:
                    return local(x)
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(11, summary.CyclomaticComplexitySum);
        Assert.Equal(6, summary.CyclomaticComplexityMax);
        Assert.Equal(3, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal("Run", callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Function, callable.Kind);
                Assert.Equal("top", callable.Name);
                Assert.Equal(6, callable.CyclomaticComplexity);
                Assert.Equal(3, callable.MaxNestingDepth);
                Assert.Equal(3, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Closure, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsGroupedAndVariadicParametersAsSeparateParameters()
    {
        const string sourceText = """
            package sample

            func top(x, y int, values ...string) {}
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(3, callable.ParameterCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotLetNestedClosureComplexityLeakIntoParentCallable()
    {
        const string sourceText = """
            package sample

            func top() int {
                local := func(x int) int {
                    if x > 0 {
                        return x
                    }

                    return 0
                }

                return 1
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

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
            package sample

            func top(x int) int {
                if x == 0 {
                    return 0
                } else if x == 1 {
                    return 1
                } else if x == 2 {
                    return 2
                }

                return 3
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(4, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsTypeSwitchArmButNotDefaultArm()
    {
        const string sourceText = """
            package sample

            func top(v any) int {
                switch v.(type) {
                case int:
                    return 1
                default:
                    return 0
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(2, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSelectCaseButNotDefaultArm()
    {
        const string sourceText = """
            package sample

            func top(ch chan int) int {
                select {
                case value := <-ch:
                    return value
                default:
                    return 0
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.go", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(2, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            package sample

            func top(x int {
                return x
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.go", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }

}
