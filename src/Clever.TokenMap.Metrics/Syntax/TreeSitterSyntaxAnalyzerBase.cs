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
