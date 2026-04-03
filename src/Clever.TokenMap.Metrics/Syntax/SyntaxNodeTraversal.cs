using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax;

internal static class SyntaxNodeTraversal
{
    public static void Traverse(Node node, Action<Node> visitor)
    {
        visitor(node);
        foreach (var child in node.Children)
        {
            Traverse(child, visitor);
        }
    }

    public static List<TextSpan> CollectTextSpans(Node rootNode, Func<Node, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var spans = new List<TextSpan>();
        Traverse(rootNode, node =>
        {
            if (predicate(node))
            {
                spans.Add(new TextSpan(node.StartIndex, node.EndIndex));
            }
        });

        return spans;
    }
}
