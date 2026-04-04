using Clever.TokenMap.Core.Analysis.Syntax;
using Clever.TokenMap.Metrics.Syntax.JavaScriptLike;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.TypeScript;

public sealed class TypeScriptSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".ts", ".mts", ".cts"];

    public override string LanguageId => "typescript";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "typescript";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = JavaScriptLikeCallableMetricsWalker.CollectCallables(tree.RootNode);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables);
    }
}
