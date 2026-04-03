using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Php;

public sealed class PhpSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".php", ".phtml"];

    public override string LanguageId => "php";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "php";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = PhpCallableMetricsWalker.CollectCallables(tree.RootNode);
        var typeCount = CountNodes(tree.RootNode, IsCountedTypeDeclaration);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables, typeCount);
    }

    private static bool IsCountedTypeDeclaration(Node node) =>
        node.Type is "class_declaration" or "interface_declaration" or "enum_declaration" &&
        !HasAncestor(node, PhpCallableMetricsWalker.IsCallable);
}
