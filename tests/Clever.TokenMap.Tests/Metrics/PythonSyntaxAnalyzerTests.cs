using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.Python;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class PythonSyntaxAnalyzerTests
{
    private readonly PythonSyntaxAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_ClassifiesCommentOnlyLinesWithoutCountingTrailingComments()
    {
        const string sourceText = """
            # lead comment
            def top():
                # body comment
                return 0  # trailing
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(2, summary.CommentLineCount);
        Assert.Equal(2, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CollectsCallablesAndComplexity()
    {
        const string sourceText = """
            class Box:
                def __init__(self, value):
                    self.value = value

                def run(self, a):
                    match a:
                        case 1 if a > 0:
                            return a
                        case _:
                            return 0

            def top(x, items):
                def local(y):
                    if y > 0 and y < 10:
                        return y
                    return 0

                fn = lambda z: z if z > 0 else 0

                try:
                    for item in items:
                        if item > 0:
                            return item
                        elif item < 0:
                            return x
                except Exception:
                    return 0

                return local(x)
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Full, summary.ParseQuality);
        Assert.Equal(5, summary.FunctionCount);
        Assert.Equal(1, summary.TypeCount);
        Assert.Equal(14, summary.CyclomaticComplexitySum);
        Assert.Equal(5, summary.CyclomaticComplexityMax);
        Assert.Equal(2, summary.MaxNestingDepth);

        Assert.Collection(
            summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based),
            callable =>
            {
                Assert.Equal(CallableKind.Constructor, callable.Kind);
                Assert.Equal("__init__", callable.Name);
                Assert.Equal(1, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(2, callable.ParameterCount);
            },
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
                Assert.Equal(CallableKind.Lambda, callable.Kind);
                Assert.Null(callable.Name);
                Assert.Equal(2, callable.CyclomaticComplexity);
                Assert.Equal(0, callable.MaxNestingDepth);
                Assert.Equal(1, callable.ParameterCount);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_CountsZeroParameterLambda()
    {
        const string sourceText = """
            value = lambda: 0
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(CallableKind.Lambda, callable.Kind);
        Assert.Equal(0, callable.ParameterCount);
        Assert.Equal(1, callable.CyclomaticComplexity);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsRecoveredForBrokenSyntax()
    {
        const string sourceText = """
            def top(x
                return x
            """;

        var summary = await _analyzer.AnalyzeAsync("broken.py", sourceText, CancellationToken.None);

        Assert.Equal(SyntaxParseQuality.Recovered, summary.ParseQuality);
    }

    [Fact]
    public async Task AnalyzeAsync_TreatsDocstringsAsCodeNotComments()
    {
        const string sourceText = """
            def top():
                '''Module-like docstring.'''
                return 0
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        Assert.Equal(0, summary.CommentLineCount);
        Assert.Equal(3, summary.CodeLineCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotLetNestedFunctionComplexityLeakIntoParentCallable()
    {
        const string sourceText = """
            def top():
                def local(x):
                    if x > 0:
                        return x
                    return 0

                return 1
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        Assert.Equal(2, summary.FunctionCount);

        var callables = summary.Callables.OrderBy(callable => callable.Lines.StartLine1Based).ToArray();
        Assert.Equal(1, callables[0].CyclomaticComplexity);
        Assert.Equal(0, callables[0].MaxNestingDepth);
        Assert.Equal(2, callables[1].CyclomaticComplexity);
        Assert.Equal(1, callables[1].MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_TreatsElifChainAsSingleNestingLevel()
    {
        const string sourceText = """
            def top(x):
                if x == 0:
                    return 0
                elif x == 1:
                    return 1
                elif x == 2:
                    return 2

                return 3
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(4, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsGuardedMatchArmButNotWildcardCatchAll()
    {
        const string sourceText = """
            def top(x):
                match x:
                    case 1 if x > 0:
                        return 1
                    case _:
                        return 0
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        var callable = Assert.Single(summary.Callables);
        Assert.Equal(3, callable.CyclomaticComplexity);
        Assert.Equal(1, callable.MaxNestingDepth);
    }

    [Fact]
    public async Task AnalyzeAsync_CountsTopLevelClassesButNotLocalClasses()
    {
        const string sourceText = """
            class TopLevel:
                pass

            def top():
                class Local:
                    pass
                return Local()
            """;

        var summary = await _analyzer.AnalyzeAsync("sample.py", sourceText, CancellationToken.None);

        Assert.Equal(1, summary.TypeCount);
    }
}
