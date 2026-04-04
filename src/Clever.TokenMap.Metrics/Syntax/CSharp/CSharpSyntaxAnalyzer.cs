using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.CSharp;

public sealed class CSharpSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".cs"];

    public override string LanguageId => "csharp";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "c-sharp";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = CSharpCallableMetricsWalker.CollectCallables(tree.RootNode);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables);
    }
}
