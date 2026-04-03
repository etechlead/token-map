using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Python;

public sealed class PythonSyntaxAnalyzer : TreeSitterSyntaxAnalyzerBase
{
    private static readonly IReadOnlyCollection<string> Extensions = [".py"];

    public override string LanguageId => "python";

    public override IReadOnlyCollection<string> FileExtensions => Extensions;

    protected override string TreeSitterLanguageId => "python";

    protected override SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var commentSpans = CollectCommentSpans(tree.RootNode);
        var lineCounts = LineClassifier.Classify(sourceText, commentSpans);
        var callables = PythonCallableMetricsWalker.CollectCallables(tree.RootNode);

        return new SyntaxSummaryArtifact(
            LanguageId,
            parseQuality,
            CodeLineCount: lineCounts.CodeLineCount,
            CommentLineCount: lineCounts.CommentLineCount,
            FunctionCount: callables.Count,
            CyclomaticComplexitySum: callables.Sum(callable => callable.CyclomaticComplexity),
            CyclomaticComplexityMax: callables.Count == 0 ? 0 : callables.Max(callable => callable.CyclomaticComplexity),
            MaxNestingDepth: callables.Count == 0 ? 0 : callables.Max(callable => callable.MaxNestingDepth),
            Callables: callables);
    }

    private static List<TextSpan> CollectCommentSpans(Node rootNode)
    {
        var commentSpans = new List<TextSpan>();
        Traverse(rootNode, node =>
        {
            if (node.Type == "comment")
            {
                commentSpans.Add(new TextSpan(node.StartIndex, node.EndIndex));
            }
        });
        return commentSpans;
    }

    private static void Traverse(Node node, Action<Node> visitor)
    {
        visitor(node);
        foreach (var child in node.Children)
        {
            Traverse(child, visitor);
        }
    }
}
