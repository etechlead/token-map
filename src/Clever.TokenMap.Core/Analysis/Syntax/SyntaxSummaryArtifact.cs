namespace Clever.TokenMap.Core.Analysis.Syntax;

public sealed record SyntaxSummaryArtifact(
    string LanguageId,
    SyntaxParseQuality ParseQuality,
    int CodeLineCount,
    int CommentLineCount,
    int FunctionCount,
    int CyclomaticComplexitySum,
    int CyclomaticComplexityMax,
    int MaxNestingDepth,
    IReadOnlyList<CallableSyntaxFact> Callables)
{
    public static SyntaxSummaryArtifact Unsupported(string languageId = "") =>
        new(
            languageId,
            SyntaxParseQuality.Unsupported,
            CodeLineCount: 0,
            CommentLineCount: 0,
            FunctionCount: 0,
            CyclomaticComplexitySum: 0,
            CyclomaticComplexityMax: 0,
            MaxNestingDepth: 0,
            Callables: []);

    public static SyntaxSummaryArtifact Failed(string languageId = "") =>
        new(
            languageId,
            SyntaxParseQuality.Failed,
            CodeLineCount: 0,
            CommentLineCount: 0,
            FunctionCount: 0,
            CyclomaticComplexitySum: 0,
            CyclomaticComplexityMax: 0,
            MaxNestingDepth: 0,
            Callables: []);
}
