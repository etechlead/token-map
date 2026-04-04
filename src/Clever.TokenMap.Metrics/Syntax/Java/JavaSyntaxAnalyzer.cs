using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Java;

public sealed class JavaSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".java"];

    public override string LanguageId => "java";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "java";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = JavaCallableMetricsWalker.CollectCallables(tree.RootNode);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables);
    }

    protected override bool IsCommentNode(Node node) =>
        node.Type is "line_comment" or "block_comment";
}
