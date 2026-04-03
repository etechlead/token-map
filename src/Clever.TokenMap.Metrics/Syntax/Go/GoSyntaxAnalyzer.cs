using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Go;

public sealed class GoSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".go"];

    public override string LanguageId => "go";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "go";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callables = GoCallableMetricsWalker.CollectCallables(tree.RootNode);
        var typeCount = CountNodes(tree.RootNode, IsCountedTypeDeclaration);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables, typeCount);
    }

    private static bool IsCountedTypeDeclaration(Node node) =>
        node.Type == "type_spec" &&
        !HasAliasChild(node) &&
        !HasAncestor(node, GoCallableMetricsWalker.IsCallable);

    private static bool HasAliasChild(Node node) =>
        node.Children.Any(child => child.Type == "=");
}
