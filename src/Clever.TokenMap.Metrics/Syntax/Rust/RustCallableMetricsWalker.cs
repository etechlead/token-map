using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Rust;

internal static class RustCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "function_item",
        "closure_expression",
    ];

    public static bool IsCallable(Node node) => CallableNodeTypes.Contains(node.Type);

    public static IReadOnlyList<CallableSyntaxFact> CollectCallables(Node rootNode) =>
        CallableFactCollector.Collect(
            rootNode,
            TryGetCallableKind,
            TryGetCallableName,
            GetParameterCount,
            ComputeMetrics);

    private static (int CyclomaticComplexity, int MaxNestingDepth) ComputeMetrics(Node callableNode)
    {
        var walker = new Walker(callableNode);
        walker.VisitChildren(callableNode, currentDepth: 0);
        return (walker.CyclomaticComplexity, walker.MaxNestingDepth);
    }

    private static bool TryGetCallableKind(Node node, out CallableKind kind)
    {
        switch (node.Type)
        {
            case "closure_expression":
                kind = CallableKind.Closure;
                return true;
            case "function_item":
                kind = ResolveFunctionKind(node);
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static CallableKind ResolveFunctionKind(Node node)
    {
        for (var current = node.Parent; !IsNull(current); current = current!.Parent)
        {
            if (current is null)
            {
                break;
            }

            if (current.Type == "impl_item")
            {
                return CallableKind.Method;
            }

            if (IsCallable(current))
            {
                return CallableKind.LocalFunction;
            }
        }

        return CallableKind.Function;
    }

    private static string? TryGetCallableName(Node node)
    {
        if (node.Type != "function_item")
        {
            return null;
        }

        var child = node.GetChildForField("name");
        return IsNull(child) ? null : child!.Text;
    }

    private static int GetParameterCount(Node callableNode)
    {
        var parametersNode = callableNode.GetChildForField("parameters");
        if (IsNull(parametersNode))
        {
            return 0;
        }

        return parametersNode!.Type switch
        {
            "parameters" => parametersNode.Children.Count(child => child.Type is "parameter" or "self_parameter"),
            "closure_parameters" => parametersNode.Children.Count(child => child.Type is "parameter" or "identifier"),
            _ => 0,
        };
    }

    private static bool IsNull(Node? node) => node is null || node.Id == IntPtr.Zero;

    private sealed class Walker(Node callableRoot)
    {
        public int CyclomaticComplexity { get; private set; } = 1;

        public int MaxNestingDepth { get; private set; }

        public void VisitChildren(Node node, int currentDepth)
        {
            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private void Visit(Node node, int currentDepth, bool isElseIfContinuation)
        {
            if (node.Id != callableRoot.Id && IsCallable(node))
            {
                return;
            }

            switch (node.Type)
            {
                case "if_expression":
                    VisitIf(node, currentDepth, isElseIfContinuation);
                    return;
                case "for_expression":
                case "while_expression":
                case "loop_expression":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "match_expression":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "match_arm":
                    VisitMatchArm(node, currentDepth);
                    return;
                case "binary_expression":
                    if (IsShortCircuitBoolean(node))
                    {
                        CyclomaticComplexity++;
                    }
                    break;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private void VisitIf(Node node, int currentDepth, bool isElseIfContinuation)
        {
            CyclomaticComplexity++;
            var nextDepth = isElseIfContinuation
                ? currentDepth
                : currentDepth + 1;
            MaxNestingDepth = Math.Max(MaxNestingDepth, nextDepth);

            foreach (var child in node.Children)
            {
                if (child.Type == "else_clause")
                {
                    VisitElseClause(child, nextDepth);
                    continue;
                }

                Visit(child, nextDepth, isElseIfContinuation: false);
            }
        }

        private void VisitElseClause(Node node, int currentDepth)
        {
            foreach (var child in node.Children)
            {
                var isElseIfChild = child.Type == "if_expression";
                Visit(child, currentDepth, isElseIfChild);
            }
        }

        private void VisitScoped(Node node, int currentDepth, int countComplexity)
        {
            CyclomaticComplexity += countComplexity;
            var nextDepth = currentDepth + 1;
            MaxNestingDepth = Math.Max(MaxNestingDepth, nextDepth);
            foreach (var child in node.Children)
            {
                Visit(child, nextDepth, isElseIfContinuation: false);
            }
        }

        private void VisitMatchArm(Node node, int currentDepth)
        {
            if (!IsDefaultMatchArm(node))
            {
                CyclomaticComplexity++;
            }

            if (HasGuard(node))
            {
                CyclomaticComplexity++;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private static bool IsDefaultMatchArm(Node node)
        {
            var patternNode = node.Children.FirstOrDefault(child => child.Type == "match_pattern");
            return !IsNull(patternNode) &&
                   string.Equals(patternNode!.Text, "_", StringComparison.Ordinal) &&
                   !HasGuard(node);
        }

        private static bool HasGuard(Node node)
        {
            var patternNode = node.Children.FirstOrDefault(child => child.Type == "match_pattern");
            return !IsNull(patternNode) &&
                   patternNode!.Children.Any(child => child.Type == "if");
        }

        private static bool IsShortCircuitBoolean(Node node) =>
            node.Children.Any(child => child.Type is "&&" or "||");
    }
}
