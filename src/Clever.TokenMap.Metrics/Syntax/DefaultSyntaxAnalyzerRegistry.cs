using Clever.TokenMap.Metrics.Syntax.CSharp;
using Clever.TokenMap.Metrics.Syntax.TypeScript;

namespace Clever.TokenMap.Metrics.Syntax;

public static class DefaultSyntaxAnalyzerRegistry
{
    public static ISyntaxAnalyzerRegistry Instance { get; } =
        new ExtensionSyntaxAnalyzerRegistry(
        [
            new CSharpSyntaxAnalyzer(),
            new TypeScriptSyntaxAnalyzer(),
        ]);
}
