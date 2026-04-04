using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Rust;

public sealed class RustSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".rs"];

    public override string LanguageId => "rust";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "rust";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = RustCallableMetricsWalker.CollectCallables(tree.RootNode);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables);
    }

    protected override bool IsCommentNode(Node node) =>
        node.Type is "line_comment" or "block_comment";
}
