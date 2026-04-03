using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Go;

internal static class GoCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "function_declaration",
        "method_declaration",
        "func_literal",
    ];

    private static readonly HashSet<string> ParameterDeclarationNodeTypes =
    [
        "parameter_declaration",
        "variadic_parameter_declaration",
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
        kind = node.Type switch
        {
            "function_declaration" => CallableKind.Function,
            "method_declaration" => CallableKind.Method,
            "func_literal" => CallableKind.Closure,
            _ => default,
        };

        return CallableNodeTypes.Contains(node.Type);
    }

    private static string? TryGetCallableName(Node node)
    {
        return node.Type switch
        {
            "function_declaration" or "method_declaration" => TryGetFieldText(node, "name"),
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

        return parametersNode!.Children
            .Where(child => ParameterDeclarationNodeTypes.Contains(child.Type))
            .Sum(CountDeclaredParameters);
    }

    private static int CountDeclaredParameters(Node declarationNode)
    {
        var identifierCount = declarationNode.Children.Count(child => child.Type == "identifier");
        return identifierCount == 0 ? 1 : identifierCount;
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
                case "for_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "expression_switch_statement":
                case "type_switch_statement":
                case "select_statement":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "expression_case":
                case "type_case":
                case "communication_case":
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
                var isElseIfChild = child.Type == "if_statement";
                Visit(child, nextDepth, isElseIfChild);
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
