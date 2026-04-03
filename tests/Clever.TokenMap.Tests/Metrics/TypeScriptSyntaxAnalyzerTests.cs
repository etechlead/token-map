using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.TypeScript;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class TypeScriptSyntaxAnalyzerTests
{
    private readonly TypeScriptSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            // lead comment
            class Box {
              /*
               * block comment
               */
              run() {
                return 0; // trailing
              }
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.ts", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.CommentLineCount);
        Assert.Equal(5, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            class Box {
              run(a: number) {
                switch (a) {
                  case 1:
                    return a;
                  default:
                    return 0;
                }
              }
            }

            function top(x: number, items: number[]) {
              const fn = (y: number) => y > 0 ? y : 0;
              const anon = function(z: number) { return z && x ? z : x; };

              if (x > 0) {
                for (const item of items) {
                  try {
                    if (item > 0) {
                      return item;
                    } else if (item < 0) {
                      return x;
                    }
                  } catch (error) {
                    return 0;
                  }
                }
              }

              return fn(x);
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.ts", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(4, summary.FunctionCount);
        Assert.Equal(13, summary.CyclomaticComplexitySum);
        Assert.Equal(6, summary.CyclomaticComplexityMax);
        Assert.Equal(3, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
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
                Assert.Equal(6, callable.CyclomaticComplexity);
                Assert.Equal(3, callable.MaxNestingDepth);
                Assert.Equal(2, callable.ParameterCount);
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
                Assert.Equal(3, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsSingleParameterArrowFunctionWithoutParentheses()
    {
        const string sourceText = """
            const scale = x => x > 0 ? x : 0;
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.ts", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(CallableKind.Lambda, callable.Kind);
        Assert.Equal(1, callable.ParameterCount);
        Assert.Equal(2, callable.CyclomaticComplexity);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            function top(x: number {
              return x;
            }
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.ts", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }
}
