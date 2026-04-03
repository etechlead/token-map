using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax;

internal delegate bool CallableKindResolver(Node node, out CallableKind kind);

internal static class CallableFactCollector
{
    public static IReadOnlyList<CallableSyntaxFact> Collect(
        Node rootNode,
        CallableKindResolver tryGetCallableKind,
        Func<Node, string?> tryGetCallableName,
        Func<Node, int> getParameterCount,
        Func<Node, (int CyclomaticComplexity, int MaxNestingDepth)> computeMetrics)
    {
        ArgumentNullException.ThrowIfNull(tryGetCallableKind);
        ArgumentNullException.ThrowIfNull(tryGetCallableName);
        ArgumentNullException.ThrowIfNull(getParameterCount);
        ArgumentNullException.ThrowIfNull(computeMetrics);

        var callables = new List<CallableSyntaxFact>();
        SyntaxNodeTraversal.Traverse(rootNode, node =>
        {
            if (!tryGetCallableKind(node, out var callableKind))
            {
                return;
            }

            var metrics = computeMetrics(node);
            callables.Add(new CallableSyntaxFact(
                Name: tryGetCallableName(node),
                Kind: callableKind,
                Lines: new LineRange(node.StartPosition.Row + 1, node.EndPosition.Row + 1),
                ParameterCount: getParameterCount(node),
                CyclomaticComplexity: metrics.CyclomaticComplexity,
                MaxNestingDepth: metrics.MaxNestingDepth));
        });

        return callables;
    }
}
