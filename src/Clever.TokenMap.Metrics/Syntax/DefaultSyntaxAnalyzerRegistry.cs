using Clever.TokenMap.Metrics.Syntax.CSharp;

namespace Clever.TokenMap.Metrics.Syntax;

public static class DefaultSyntaxAnalyzerRegistry
{
    public static ISyntaxAnalyzerRegistry Instance { get; } =
        new ExtensionSyntaxAnalyzerRegistry(
        [
            new CSharpSyntaxAnalyzer(),
        ]);
}
