using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Php;

internal static class PhpCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "function_definition",
        "method_declaration",
        "anonymous_function",
        "arrow_function",
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
            case "method_declaration":
            {
                var methodName = TryGetCallableName(node);
                kind = string.Equals(methodName, "__construct", StringComparison.Ordinal)
                    ? CallableKind.Constructor
                    : CallableKind.Method;
                return true;
            }
            case "function_definition":
                kind = IsNestedInsideCallable(node)
                    ? CallableKind.LocalFunction
                    : CallableKind.Function;
                return true;
            case "anonymous_function":
                kind = CallableKind.Closure;
                return true;
            case "arrow_function":
                kind = CallableKind.Lambda;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static string? TryGetCallableName(Node node)
    {
        return node.Type switch
        {
            "function_definition" or "method_declaration" => TryGetFieldText(node, "name"),
            _ => null,
        };
    }

    private static int GetParameterCount(Node callableNode)
    {
        var parametersNode = callableNode.GetChildForField("parameters");
        if (IsNull(parametersNode))
        {
            return 0;
        }

        return parametersNode!.Children.Count(child => child.Type.EndsWith("_parameter", StringComparison.Ordinal));
    }

    private static bool IsNestedInsideCallable(Node node)
    {
        for (var current = node.Parent; !IsNull(current); current = current!.Parent)
        {
            if (current is not null && IsCallable(current))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryGetFieldText(Node node, string fieldName)
    {
        var child = node.GetChildForField(fieldName);
        return IsNull(child) ? null : child!.Text;
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
                case "if_statement":
                    VisitIf(node, currentDepth, isElseIfContinuation);
                    return;
                case "else_if_clause":
                    VisitIf(node, currentDepth, isElseIfContinuation: true);
                    return;
                case "for_statement":
                case "foreach_statement":
                case "while_statement":
                case "do_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "catch_clause":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "switch_statement":
                case "match_expression":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "case_statement":
                case "match_conditional_expression":
                    CyclomaticComplexity++;
                    break;
                case "conditional_expression":
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
                if (child.Type == "else_if_clause")
                {
                    Visit(child, nextDepth, isElseIfContinuation: true);
                    continue;
                }

                Visit(child, nextDepth, isElseIfContinuation: false);
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
