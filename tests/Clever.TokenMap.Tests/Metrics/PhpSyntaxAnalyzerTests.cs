using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.Php;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class PhpSyntaxAnalyzerTests
{
    private readonly PhpSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            <?php
            // lead comment
            function top(): int {
                /* block
                 * comment
                 */
                return 0; // trailing
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CommentLineCount);
        Assert.Equal(4, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            <?php

            class Box {
                public function __construct(private int $value) {}

                public function run(int $a): int {
                    switch ($a) {
                        case 1:
                            return $a;
                        default:
                            return 0;
                    }
                }
            }

            function top(int $x, array $items): int {
                function local(int $y): int {
                    if ($y > 0 && $y < 10) {
                        return $y;
                    }
                    return 0;
                }

                $anon = function(int $z): int {
                    return $z > 0 ? $z : 0;
                };

                $arrow = fn(int $q): int => $q > 0 ? $q : 0;

                try {
                    foreach ($items as $item) {
                        if ($item > 0) {
                            return $item;
                        } elseif ($item < 0) {
                            return $x;
                        }
                    }
                } catch (RuntimeException $ex) {
                    return 0;
                }

                return local($x);
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(6, summary.FunctionCount);
        Assert.Equal(1, summary.TypeCount);
        Assert.Equal(15, summary.CyclomaticComplexitySum);
        Assert.Equal(5, summary.CyclomaticComplexityMax);
        Assert.Equal(2, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Constructor, callable.Kind);
                Assert.Equal("__construct", callable.Name);
                Assert.Equal(1, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Method, callable.Kind);
                Assert.Equal("run", callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(1, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Function, callable.Kind);
                Assert.Equal("top", callable.Name);
                Assert.Equal(5, callable.CyclomaticComplexity);
                Assert.Equal(2, callable.MaxNestingDepth);
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
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsVariadicAndPropertyPromotionParameters()
    {
        const string sourceText = """
            <?php

            class Box {
                public function __construct(private int $x, protected string $y) {}
            }

            function top(int $x, string ...$rest): int {
                return $x;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Constructor, callable.Kind);
                Assert.Equal(2, callable.ParameterCount);
            },
            callable =>
            {
                Assert.Equal(CallableKind.Function, callable.Kind);
                Assert.Equal(2, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotLetNestedCallableComplexityLeakIntoParentCallable()
    {
        const string sourceText = """
            <?php

            function top(): int {
                $local = function(int $x): int {
                    if ($x > 0) {
                        return $x;
                    }
                    return 0;
                };

                return 1;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

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
            <?php

            function top(int $x): int {
                if ($x === 0) {
                    return 0;
                } elseif ($x === 1) {
                    return 1;
                } elseif ($x === 2) {
                    return 2;
                }

                return 3;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(4, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsMatchArmButNotDefaultArm()
    {
        const string sourceText = """
            <?php

            function top(int $x): int {
                return match ($x) {
                    1 => 1,
                    default => 0,
                };
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(2, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            <?php

            function top(int $x: int {
                return $x;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.php", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSupportedNamedTypesButNotTraitsOrLocalClasses()
    {
        const string sourceText = """
            <?php

            class Box {}
            interface Shape {}
            enum Kind { case Box; }
            trait SharedBox {}

            function top(): void {
                class LocalBox {}
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.php", sourceText, CancellationToken.None);

        Assert.Equal(3, summary.TypeCount);
    }
}
