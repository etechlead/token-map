using Clever.TokenMap.Metrics.Syntax.CSharp;
using Clever.TokenMap.Metrics.Syntax.Go;
using Clever.TokenMap.Metrics.Syntax.Java;
using Clever.TokenMap.Metrics.Syntax.JavaScript;
using Clever.TokenMap.Metrics.Syntax.Php;
using Clever.TokenMap.Metrics.Syntax.Python;
using Clever.TokenMap.Metrics.Syntax.TypeScript;

namespace Clever.TokenMap.Metrics.Syntax;

public static class DefaultSyntaxAnalyzerRegistry
{
    public static ISyntaxAnalyzerRegistry Instance { get; } =
        new ExtensionSyntaxAnalyzerRegistry(
        [
            new CSharpSyntaxAnalyzer(),
            new GoSyntaxAnalyzer(),
            new JavaSyntaxAnalyzer(),
            new JavaScriptSyntaxAnalyzer(),
            new PhpSyntaxAnalyzer(),
            new PythonSyntaxAnalyzer(),
            new TypeScriptSyntaxAnalyzer(),
        ]);
}
