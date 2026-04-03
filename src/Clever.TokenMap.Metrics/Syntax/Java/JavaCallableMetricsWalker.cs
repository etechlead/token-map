using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Java;

internal static class JavaCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "method_declaration",
        "constructor_declaration",
        "lambda_expression",
    ];

    private static readonly HashSet<string> FormalParameterNodeTypes =
    [
        "formal_parameter",
        "spread_parameter",
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
            "lambda_expression" => CallableKind.Lambda,
            _ => default,
        };

        return CallableNodeTypes.Contains(node.Type);
    }

    private static string? TryGetCallableName(Node node)
    {
        return node.Type switch
        {
            "method_declaration" or "constructor_declaration" => TryGetFieldText(node, "name"),
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

        return parametersNode!.Type switch
        {
            "formal_parameters" or "inferred_parameters"
                => parametersNode.Children.Count(child => FormalParameterNodeTypes.Contains(child.Type) || child.Type == "identifier"),
            "identifier" => 1,
            _ => 0,
        };
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
                case "enhanced_for_statement":
                case "while_statement":
                case "do_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "catch_clause":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "switch_expression":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "switch_block_statement_group":
                    VisitSwitchBlockStatementGroup(node, currentDepth);
                    return;
                case "switch_rule":
                    VisitSwitchRule(node, currentDepth);
                    return;
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

        private void VisitSwitchBlockStatementGroup(Node node, int currentDepth)
        {
            if (HasNonDefaultSwitchLabel(node))
            {
                CyclomaticComplexity++;
            }

            if (HasGuardedSwitchLabel(node))
            {
                CyclomaticComplexity++;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private void VisitSwitchRule(Node node, int currentDepth)
        {
            if (HasNonDefaultSwitchLabel(node))
            {
                CyclomaticComplexity++;
            }

            if (HasGuardedSwitchLabel(node))
            {
                CyclomaticComplexity++;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private static bool HasNonDefaultSwitchLabel(Node node) =>
            node.Children
                .Where(child => child.Type == "switch_label")
                .Any(label => !IsDefaultSwitchLabel(label));

        private static bool HasGuardedSwitchLabel(Node node) =>
            node.Children
                .Where(child => child.Type == "switch_label")
                .Any(label => label.Children.Any(grandChild => grandChild.Type == "guard"));

        private static bool IsDefaultSwitchLabel(Node node) =>
            node.Children.Any(child => child.Type == "default");

        private static bool IsShortCircuitBoolean(Node node) =>
            node.Children.Any(child => child.Type is "&&" or "||");
    }
}
