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
        var typeCount = CountNodes(tree.RootNode, IsCountedTypeDeclaration);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables, typeCount);
    }

    protected override bool IsCommentNode(Node node) =>
        node.Type is "line_comment" or "block_comment";

    private static bool IsCountedTypeDeclaration(Node node) =>
        node.Type is "class_declaration" or "interface_declaration" or "enum_declaration" or "record_declaration" &&
        !HasAncestor(node, JavaCallableMetricsWalker.IsCallable);
}
