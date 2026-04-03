namespace Clever.TokenMap.Metrics.Syntax;

public interface ISyntaxAnalyzerRegistry
{
    bool TryResolve(string fullPath, out ISyntaxAnalyzer analyzer);
}
