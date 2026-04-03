using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.CSharp;

internal static class CSharpCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "method_declaration",
        "constructor_declaration",
        "local_function_statement",
        "lambda_expression",
        "anonymous_method_expression",
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
            "method_declaration" => CallableKind.Method,
            "constructor_declaration" => CallableKind.Constructor,
            "local_function_statement" => CallableKind.LocalFunction,
            "lambda_expression" => CallableKind.Lambda,
            "anonymous_method_expression" => CallableKind.Closure,
            _ => default,
        };

        return CallableNodeTypes.Contains(node.Type);
    }

    private static string? TryGetCallableName(Node node)
    {
        return node.Type switch
        {
            "method_declaration" or "constructor_declaration" or "local_function_statement"
                => TryGetFieldText(node, "name"),
            _ => null,
        };
    }

    private static int GetParameterCount(Node callableNode)
    {
        var parametersNode = callableNode.GetChildForField("parameters")!;
        if (IsNull(parametersNode))
        {
            return 0;
        }

        return parametersNode.Type switch
        {
            "parameter_list" => parametersNode.Children.Count(child => child.Type == "parameter"),
            "implicit_parameter" => 1,
            _ => 0,
        };
    }

    private static string? TryGetFieldText(Node node, string fieldName)
    {
        var child = node.GetChildForField(fieldName)!;
        return IsNull(child) ? null : child.Text;
    }

    private static bool IsNull(Node node) => node.Id == IntPtr.Zero;

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
                case "foreach_statement":
                case "while_statement":
                case "do_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "catch_clause":
                    VisitCatch(node, currentDepth);
                    return;
                case "switch_statement":
                case "switch_expression":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "switch_section":
                    VisitSwitchSection(node, currentDepth);
                    return;
                case "switch_expression_arm":
                    VisitSwitchExpressionArm(node, currentDepth);
                    return;
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
            var alternativeNode = node.GetChildForField("alternative")!;

            foreach (var child in node.Children)
            {
                var isElseIfChild =
                    child.Type == "if_statement" &&
                    !IsNull(alternativeNode) &&
                    child.Id == alternativeNode.Id;
                Visit(child, nextDepth, isElseIfChild);
            }
        }

        private void VisitCatch(Node node, int currentDepth)
        {
            CyclomaticComplexity++;
            if (HasDirectChild(node, "catch_filter_clause"))
            {
                CyclomaticComplexity++;
            }

            var nextDepth = currentDepth + 1;
            MaxNestingDepth = Math.Max(MaxNestingDepth, nextDepth);
            foreach (var child in node.Children)
            {
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

        private void VisitSwitchSection(Node node, int currentDepth)
        {
            var armCount = CountSwitchSectionArms(node);
            CyclomaticComplexity += armCount;
            if (armCount > 0 && HasDirectChild(node, "when_clause"))
            {
                CyclomaticComplexity++;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private void VisitSwitchExpressionArm(Node node, int currentDepth)
        {
            if (!IsDefaultSwitchExpressionArm(node))
            {
                CyclomaticComplexity++;
                if (HasDirectChild(node, "when_clause"))
                {
                    CyclomaticComplexity++;
                }
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private static int CountSwitchSectionArms(Node node) =>
            node.Children.Count(IsSwitchSectionPatternNode);

        private static bool IsSwitchSectionPatternNode(Node node) =>
            node.Type == "discard" ||
            node.Type.EndsWith("_pattern", StringComparison.Ordinal);

        private static bool IsDefaultSwitchExpressionArm(Node node) =>
            HasDirectChild(node, "discard") &&
            !HasDirectChild(node, "when_clause");

        private static bool HasDirectChild(Node node, string type) =>
            node.Children.Any(child => child.Type == type);

        private static bool IsShortCircuitBoolean(Node node) =>
            node.Children.Any(child => child.Type is "&&" or "||");
    }
}
