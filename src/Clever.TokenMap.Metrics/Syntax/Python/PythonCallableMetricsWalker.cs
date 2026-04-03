using Clever.TokenMap.Core.Analysis.Syntax;
using TreeSitter;

namespace Clever.TokenMap.Metrics.Syntax.Python;

internal static class PythonCallableMetricsWalker
{
    private static readonly HashSet<string> CallableNodeTypes =
    [
        "function_definition",
        "lambda",
    ];

    private static readonly HashSet<string> ParameterPunctuationNodeTypes =
    [
        "(",
        ")",
        ",",
        "/",
        "*",
        "**",
    ];

    public static bool IsCallable(Node node) => node.IsNamed && CallableNodeTypes.Contains(node.Type);

    public static IReadOnlyList<CallableSyntaxFact> CollectCallables(Node rootNode)
    {
        var callables = new List<CallableSyntaxFact>();
        Traverse(rootNode, node =>
        {
            if (!TryGetCallableKind(node, out var callableKind))
            {
                return;
            }

            var metrics = ComputeMetrics(node);
            callables.Add(new CallableSyntaxFact(
                Name: TryGetCallableName(node),
                Kind: callableKind,
                Lines: new LineRange(node.StartPosition.Row + 1, node.EndPosition.Row + 1),
                ParameterCount: GetParameterCount(node),
                CyclomaticComplexity: metrics.CyclomaticComplexity,
                MaxNestingDepth: metrics.MaxNestingDepth));
        });

        return callables;
    }

    private static ComplexityMetrics ComputeMetrics(Node callableNode)
    {
        var walker = new Walker(callableNode);
        walker.VisitChildren(callableNode, currentDepth: 0);
        return new ComplexityMetrics(walker.CyclomaticComplexity, walker.MaxNestingDepth);
    }

    private static bool TryGetCallableKind(Node node, out CallableKind kind)
    {
        if (!node.IsNamed)
        {
            kind = default;
            return false;
        }

        if (node.Type == "lambda")
        {
            kind = CallableKind.Lambda;
            return true;
        }

        if (node.Type != "function_definition")
        {
            kind = default;
            return false;
        }

        var name = TryGetCallableName(node);
        for (var current = node.Parent; !IsNull(current); current = current!.Parent)
        {
            if (current!.Type == "class_definition")
            {
                kind = string.Equals(name, "__init__", StringComparison.Ordinal)
                    ? CallableKind.Constructor
                    : CallableKind.Method;
                return true;
            }

            if (IsCallable(current))
            {
                kind = CallableKind.LocalFunction;
                return true;
            }
        }

        kind = CallableKind.Function;
        return true;
    }

    private static string? TryGetCallableName(Node node)
    {
        var child = node.GetChildForField("name");
        return IsNull(child) ? null : child!.Text;
    }

    private static int GetParameterCount(Node callableNode)
    {
        var parametersNode = callableNode.GetChildForField("parameters");
        if (IsNull(parametersNode))
        {
            parametersNode = callableNode.Children.FirstOrDefault(child => child.Type == "lambda_parameters");
        }

        return IsNull(parametersNode)
            ? 0
            : parametersNode!.Children.Count(IsParameterNode);
    }

    private static bool IsParameterNode(Node node) => !ParameterPunctuationNodeTypes.Contains(node.Type);

    private static bool IsNull(Node? node) => node is null || node.Id == IntPtr.Zero;

    private static void Traverse(Node node, Action<Node> visitor)
    {
        visitor(node);
        foreach (var child in node.Children)
        {
            Traverse(child, visitor);
        }
    }

    private readonly record struct ComplexityMetrics(int CyclomaticComplexity, int MaxNestingDepth);

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
                case "elif_clause":
                    VisitIf(node, currentDepth, isElseIfContinuation: true);
                    return;
                case "for_statement":
                case "while_statement":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "except_clause":
                    VisitScoped(node, currentDepth, countComplexity: 1);
                    return;
                case "match_statement":
                    VisitScoped(node, currentDepth, countComplexity: 0);
                    return;
                case "case_clause":
                    VisitCaseClause(node, currentDepth);
                    return;
                case "conditional_expression":
                    CyclomaticComplexity++;
                    break;
                case "boolean_operator":
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
                if (child.Type == "elif_clause")
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

        private void VisitCaseClause(Node node, int currentDepth)
        {
            if (!IsDefaultCaseClause(node))
            {
                CyclomaticComplexity++;
            }

            if (HasDirectChild(node, "if_clause"))
            {
                CyclomaticComplexity++;
            }

            foreach (var child in node.Children)
            {
                Visit(child, currentDepth, isElseIfContinuation: false);
            }
        }

        private static bool IsDefaultCaseClause(Node node)
        {
            var patternNode = node.Children.FirstOrDefault(child => child.Type == "case_pattern");
            return !IsNull(patternNode) &&
                   string.Equals(patternNode!.Text, "_", StringComparison.Ordinal) &&
                   !HasDirectChild(node, "if_clause");
        }

        private static bool HasDirectChild(Node node, string type) =>
            node.Children.Any(child => child.Type == type);

        private static bool IsShortCircuitBoolean(Node node) =>
            node.Children.Any(child => child.Type is "and" or "or");
    }
}
