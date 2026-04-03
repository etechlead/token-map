using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.JavaScriptLike;

internal static class JavaScriptLikeCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "function_declaration",
        "function_expression",
        "arrow_function",
        "method_definition",
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
        if (node.Type == "method_definition")
        {
            var methodName = TryGetFieldText(node, "name");
            kind = string.Equals(methodName, "constructor", StringComparison.Ordinal)
                ? CallableKind.Constructor
                : CallableKind.Method;
            return true;
        }

        kind = node.Type switch
        {
            "function_declaration" => CallableKind.Function,
            "function_expression" => CallableKind.Closure,
            "arrow_function" => CallableKind.Lambda,
            _ => default,
        };

        return CallableNodeTypes.Contains(node.Type);
    }

    private static string? TryGetCallableName(Node node)
    {
        return node.Type switch
        {
            "function_declaration" or "function_expression" or "method_definition"
                => TryGetFieldText(node, "name"),
            _ => null,
        };
    }

    private static int GetParameterCount(Node callableNode)
    {
        var parametersNode = callableNode.GetChildForField("parameters");
        if (!IsNull(parametersNode))
        {
            return parametersNode!.Children.Count(IsFormalParameterChild);
        }

        if (callableNode.Type != "arrow_function")
        {
            return 0;
        }

        foreach (var child in callableNode.Children)
        {
            if (child.Type == "=>")
            {
                break;
            }

            if (IsDirectArrowParameterNode(child))
            {
                return 1;
            }
        }

        return 0;
    }

    private static string? TryGetFieldText(Node node, string fieldName)
    {
        var child = node.GetChildForField(fieldName);
        return IsNull(child) ? null : child!.Text;
    }

    private static bool IsFormalParameterChild(Node node) =>
        node.Type is not "(" and not ")" and not ",";

    private static bool IsDirectArrowParameterNode(Node node) =>
        node.Type is "identifier" or "object_pattern" or "array_pattern" or "assignment_pattern" or "required_parameter";

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
                case "if_statement":
                    VisitIf(node, currentDepth, isElseIfContinuation);
                    return;
                case "for_statement":
                case "for_in_statement":
                case "while_statement":
                case "do_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "catch_clause":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "switch_statement":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "switch_case":
                    CyclomaticComplexity++;
                    break;
                case "ternary_expression":
                    CyclomaticComplexity++;
                    break;
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
                var isElseIfChild = child.Type == "if_statement";
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

        private static bool IsShortCircuitBoolean(Node node) =>
            node.Children.Any(child => child.Type is "&&" or "||");
    }
}
