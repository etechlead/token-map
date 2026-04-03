using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax;

public abstract class TreeSitterSyntaxAnalyzerBase : ISyntaxAnalyzer
{
    public abstract string LanguageId { get; }

    public abstract IReadOnlyCollection<string> FileExtensions { get; }

    protected abstract string TreeSitterLanguageId { get; }

    public virtual bool CanAnalyze(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        return FileExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<SyntaxSummaryArtifact> AnalyzeAsync(
        string fullPath,
        string sourceText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(sourceText);
        cancellationToken.ThrowIfCancellationRequested();

        using var language = CreateLanguage();
        using var parser = new Parser(language);
        using var tree = parser.Parse(sourceText);
        if (tree is null)
        {
            return ValueTask.FromResult(SyntaxSummaryArtifact.Failed(LanguageId));
        }

        var parseQuality = DetermineParseQuality(tree.RootNode);
        return ValueTask.FromResult(CreateSummary(tree, parseQuality, sourceText, cancellationToken));
    }

    protected virtual Language CreateLanguage() => new(TreeSitterLanguageId);

    protected abstract SyntaxSummaryArtifact CreateSummary(
        Tree tree,
        SyntaxParseQuality parseQuality,
        string sourceText,
        CancellationToken cancellationToken);

    protected SyntaxSummaryArtifact CreateStandardSummary(
        Node rootNode,
        SyntaxParseQuality parseQuality,
        string sourceText,
        IReadOnlyList<CallableSyntaxFact> callables,
        int typeCount = 0)
    {
        var commentSpans = SyntaxNodeTraversal.CollectTextSpans(rootNode, IsCommentNode);
        var lineCounts = LineClassifier.Classify(sourceText, commentSpans);

        return new SyntaxSummaryArtifact(
            LanguageId,
            parseQuality,
            CodeLineCount: lineCounts.CodeLineCount,
            CommentLineCount: lineCounts.CommentLineCount,
            FunctionCount: callables.Count,
            TypeCount: typeCount,
            CyclomaticComplexitySum: callables.Sum(callable => callable.CyclomaticComplexity),
            CyclomaticComplexityMax: callables.Count == 0 ? 0 : callables.Max(callable => callable.CyclomaticComplexity),
            MaxNestingDepth: callables.Count == 0 ? 0 : callables.Max(callable => callable.MaxNestingDepth),
            Callables: callables);
    }

    protected virtual bool IsCommentNode(Node node) => node.Type == "comment";

    protected static int CountNodes(Node rootNode, Func<Node, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var count = 0;
        SyntaxNodeTraversal.Traverse(rootNode, node =>
        {
            if (predicate(node))
            {
                count++;
            }
        });

        return count;
    }

    protected static bool HasAncestor(Node node, Func<Node, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        for (var current = node.Parent; current is not null && current.Id != IntPtr.Zero; current = current.Parent)
        {
            if (predicate(current))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxParseQuality DetermineParseQuality(Node rootNode)
    {
        var stack = new Stack<Node>();
        stack.Push(rootNode);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.IsError || current.IsMissing)
            {
                return SyntaxParseQuality.Recovered;
            }

            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }

        return SyntaxParseQuality.Full;
    }
}
