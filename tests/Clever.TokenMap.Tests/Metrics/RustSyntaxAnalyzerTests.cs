using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.Rust;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class RustSyntaxAnalyzerTests
{
    private readonly RustSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            // lead comment
            fn top() -> i32 {
                /* block
                 * comment
                 */
                0 // trailing
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CommentLineCount);
        Assert.Equal(3, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            struct Box;

            impl Box {
                fn run(&self, x: i32) -> i32 {
                    match x {
                        1 if x > 0 => x,
                        _ => 0,
                    }
                }
            }

            fn top(x: i32, items: &[i32]) -> i32 {
                fn local(y: i32) -> i32 {
                    if y > 0 && y < 10 {
                        y
                    } else {
                        0
                    }
                }

                let c = |z: i32| if z > 0 { z } else { 0 };

                if x == 0 {
                    0
                } else if x == 1 {
                    1
                } else {
                    for item in items {
                        if *item > 0 {
                            return *item;
                        }
                    }

                    loop {
                        break;
                    }

                    local(x)
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.FunctionCount);
        Assert.Equal(1, summary.TypeCount);
        Assert.Equal(14, summary.CyclomaticComplexitySum);
        Assert.Equal(6, summary.CyclomaticComplexityMax);
        Assert.Equal(3, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal("run", callable.Name);
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(2, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Function, callable.Kind);
                Assert.Equal("top", callable.Name);
                Assert.Equal(6, callable.CyclomaticComplexity);
                Assert.Equal(3, callable.MaxNestingDepth);
                Assert.Equal(2, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.LocalFunction, callable.Kind);
                Assert.Equal("local", callable.Name);
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Closure, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSelfAndClosureParameters()
    {
        const string sourceText = """
            struct Box;

            impl Box {
                fn run(&self, x: i32) -> i32 { x }
            }

            fn top() {
                let a = |x: i32| x;
                let b = |y| y;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal(2, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Function, callable.Kind);
                Assert.Equal(0, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Closure, callable.Kind);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Closure, callable.Kind);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotLetNestedCallableComplexityLeakIntoParentCallable()
    {
        const string sourceText = """
            fn top() -> i32 {
                let local = |x: i32| {
                    if x > 0 {
                        x
                    } else {
                        0
                    }
                };

                1
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

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
            fn top(x: i32) -> i32 {
                if x == 0 {
                    0
                } else if x == 1 {
                    1
                } else if x == 2 {
                    2
                } else {
                    3
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(4, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsGuardedMatchArmButNotWildcardArm()
    {
        const string sourceText = """
            fn top(x: i32) -> i32 {
                match x {
                    1 if x > 0 => x,
                    _ => 0,
                }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(3, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            fn top(x: i32 -> i32 {
                x
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.rs", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSupportedNamedTypesButNotAliasesOrLocalTypes()
    {
        const string sourceText = """
            struct Box;
            enum Kind { Box }
            trait Shape {}
            union Value { int_value: i32 }
            type Alias = i32;

            fn top() {
                struct Local;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.rs", sourceText, CancellationToken.None);

        Assert.Equal(4, summary.TypeCount);
    }
}
