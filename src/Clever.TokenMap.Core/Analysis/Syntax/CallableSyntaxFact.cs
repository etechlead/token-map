namespace Clever.TokenMap.Core.Analysis.Syntax;

public sealed record CallableSyntaxFact(
    string? Name,
    CallableKind Kind,
    LineRange Lines,
    int ParameterCount,
    int CyclomaticComplexity,
    int MaxNestingDepth);
