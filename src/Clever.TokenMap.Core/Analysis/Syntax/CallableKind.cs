namespace Clever.TokenMap.Core.Analysis.Syntax;

public enum CallableKind
{
    Function = 0,
    Method = 1,
    Constructor = 2,
    LocalFunction = 3,
    Lambda = 4,
    Closure = 5,
}
