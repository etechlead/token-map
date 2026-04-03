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
        var typeCount = CountNodes(tree.RootNode, IsCountedTypeDeclaration);
        return CreateStandardSummary(tree.RootNode, parseQuality, sourceText, callables, typeCount);
    }

    private static bool IsCountedTypeDeclaration(Node node) =>
        node.Type is "class_declaration"
            or "struct_declaration"
            or "interface_declaration"
            or "enum_declaration"
            or "record_declaration"
            or "record_struct_declaration";
}
