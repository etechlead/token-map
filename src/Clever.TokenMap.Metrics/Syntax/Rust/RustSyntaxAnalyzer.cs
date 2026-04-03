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
        var typeCount = CountNodes(tree.RootNode, IsCountedTypeDeclaration);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables, typeCount);
    }

    protected override bool IsCommentNode(Node node) =>
        node.Type is "line_comment" or "block_comment";

    private static bool IsCountedTypeDeclaration(Node node) =>
        node.Type is "struct_item" or "enum_item" or "trait_item" or "union_item" &&
        !HasAncestor(node, RustCallableMetricsWalker.IsCallable);
}
