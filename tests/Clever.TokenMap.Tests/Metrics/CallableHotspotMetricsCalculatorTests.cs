using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Core.Metrics;
using Clever.TokenMap.Metrics;
using Clever.TokenMap.Metrics.Calculators.Derived;
using Clever.TokenMap.Metrics.Syntax.CSharp;
using Clever.TokenMap.Metrics.Syntax.Python;
using Clever.TokenMap.Metrics.Syntax.TypeScript;

namespace Clever.TokenMap.Tests.Metrics;

public sealed class CallableHotspotMetricsCalculatorTests
{
    private readonly CallableHotspotMetricsCalculator _calculator = new();

    [Fact]
    public async Task ComputeAsync_ReturnsZeroAndNotApplicableForEmptyCallableList()
    {
        var result = await ComputeAsync(new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 0,
            CommentLineCount: 0,
            FunctionCount: 0,
            TypeCount: 0,
            CyclomaticComplexitySum: 0,
            CyclomaticComplexityMax: 0,
            MaxNestingDepth: 0,
            Callables: []));

        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.MaxCallableLines));
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.AverageCallableLines).Status);
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.CallableHotspotPointsV0));
    }

    [Fact]
    public async Task ComputeAsync_ComputesMetricsForSingleSimpleCallable()
    {
        var result = await ComputeAsync(new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 3,
            CommentLineCount: 0,
            FunctionCount: 1,
            TypeCount: 1,
            CyclomaticComplexitySum: 1,
            CyclomaticComplexityMax: 1,
            MaxNestingDepth: 0,
            Callables:
            [
                new CallableSyntaxFact("Run", CallableKind.Method, new LineRange(10, 12), 2, 1, 0),
            ]));

        Assert.Equal(3, result.TryGetRoundedInt32(MetricIds.MaxCallableLines));
        Assert.Equal(3d, result.TryGetNumber(MetricIds.AverageCallableLines)!.Value, precision: 12);
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(0, result.TryGetRoundedInt32(MetricIds.CallableHotspotPointsV0));
    }

    [Fact]
    public async Task ComputeAsync_CountsLongCallableAndHotspots()
    {
        var result = await ComputeAsync(new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 35,
            CommentLineCount: 0,
            FunctionCount: 1,
            TypeCount: 1,
            CyclomaticComplexitySum: 12,
            CyclomaticComplexityMax: 12,
            MaxNestingDepth: 5,
            Callables:
            [
                new CallableSyntaxFact("Run", CallableKind.Method, new LineRange(20, 50), 5, 12, 5),
            ]));

        Assert.Equal(31, result.TryGetRoundedInt32(MetricIds.MaxCallableLines));
        Assert.Equal(31d, result.TryGetNumber(MetricIds.AverageCallableLines)!.Value, precision: 12);
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(8, result.TryGetRoundedInt32(MetricIds.CallableHotspotPointsV0));
    }

    [Fact]
    public async Task ComputeAsync_ComputesIndependentCountersAcrossMultipleCallables()
    {
        var result = await ComputeAsync(new SyntaxSummaryArtifact(
            LanguageId: "csharp",
            ParseQuality: SyntaxParseQuality.Full,
            CodeLineCount: 70,
            CommentLineCount: 0,
            FunctionCount: 4,
            TypeCount: 1,
            CyclomaticComplexitySum: 25,
            CyclomaticComplexityMax: 11,
            MaxNestingDepth: 5,
            Callables:
            [
                new CallableSyntaxFact("A", CallableKind.Method, new LineRange(1, 10), 2, 3, 1),
                new CallableSyntaxFact("B", CallableKind.Method, new LineRange(12, 42), 1, 11, 2),
                new CallableSyntaxFact("C", CallableKind.Method, new LineRange(45, 55), 6, 4, 5),
                new CallableSyntaxFact("D", CallableKind.Method, new LineRange(57, 88), 7, 14, 4),
            ]));

        Assert.Equal(32, result.TryGetRoundedInt32(MetricIds.MaxCallableLines));
        Assert.Equal(21d, result.TryGetNumber(MetricIds.AverageCallableLines)!.Value, precision: 12);
        Assert.Equal(2, result.TryGetRoundedInt32(MetricIds.LongCallableCount));
        Assert.Equal(2, result.TryGetRoundedInt32(MetricIds.HighCyclomaticComplexityCallableCount));
        Assert.Equal(2, result.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(2, result.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(16, result.TryGetRoundedInt32(MetricIds.CallableHotspotPointsV0));
    }

    [Fact]
    public async Task ComputeAsync_SetsNotApplicableWhenSyntaxIsUnsupported()
    {
        var result = await ComputeAsync(SyntaxSummaryArtifact.Unsupported("csharp"));

        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.MaxCallableLines).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.AverageCallableLines).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.LongCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.HighCyclomaticComplexityCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.DeepNestingCallableCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.LongParameterListCount).Status);
        Assert.Equal(MetricStatus.NotApplicable, result.GetOrDefault(MetricIds.CallableHotspotPointsV0).Status);
    }

    [Theory]
    [InlineData("sample.cs", "csharp")]
    [InlineData("sample.ts", "typescript")]
    [InlineData("sample.py", "python")]
    public async Task ComputeAsync_SmokeTestsAcrossSupportedLanguages(string fileName, string language)
    {
        var sourceText = language switch
        {
            "csharp" => """
                class Sample
                {
                    int Heavy(int a, int b, int c, int d, int e)
                    {
                        if (a > 0)
                        {
                            if (b > 0)
                            {
                                if (c > 0)
                                {
                                    if (d > 0)
                                    {
                                        return a + b + c + d + e;
                                    }
                                }
                            }
                        }

                        return 0;
                    }
                }
                """,
            "typescript" => """
                function heavy(a: number, b: number, c: number, d: number, e: number) {
                  if (a > 0) {
                    if (b > 0) {
                      if (c > 0) {
                        if (d > 0) {
                          return a + b + c + d + e;
                        }
                      }
                    }
                  }

                  return 0;
                }
                """,
            "python" => """
                def heavy(a, b, c, d, e):
                    if a > 0:
                        if b > 0:
                            if c > 0:
                                if d > 0:
                                    return a + b + c + d + e

                    return 0
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        var summary = language switch
        {
            "csharp" => await new CSharpSyntaxAnalyzer().AnalyzeAsync(fileName, sourceText, CancellationToken.None),
            "typescript" => await new TypeScriptSyntaxAnalyzer().AnalyzeAsync(fileName, sourceText, CancellationToken.None),
            "python" => await new PythonSyntaxAnalyzer().AnalyzeAsync(fileName, sourceText, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };
        var result = await ComputeAsync(summary);

        Assert.True(result.TryGetRoundedInt32(MetricIds.MaxCallableLines) >= 7);
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.DeepNestingCallableCount));
        Assert.Equal(1, result.TryGetRoundedInt32(MetricIds.LongParameterListCount));
        Assert.Equal(3, result.TryGetRoundedInt32(MetricIds.CallableHotspotPointsV0));
    }

    private async Task<MetricSet> ComputeAsync(SyntaxSummaryArtifact? syntaxSummary)
    {
        var builder = new MetricSetBuilder();

        await _calculator.ComputeAsync(
            new StubFileMetricContext(syntaxSummary),
            MetricSet.Empty,
            builder,
            CancellationToken.None);

        return builder.Build();
    }

    private sealed class StubFileMetricContext(SyntaxSummaryArtifact? syntaxSummary) : IFileMetricContext
    {
        public long FileSizeBytes => 0;

        public ValueTask<TArtifact?> GetArtifactAsync<TArtifact>(CancellationToken cancellationToken)
            where TArtifact : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(syntaxSummary as TArtifact);
        }
    }
}
