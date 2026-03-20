namespace Clever.TokenMap.Core.Enums;

public enum SkippedReason
{
    Excluded = 0,
    ReparsePoint = 1,
    Inaccessible = 2,
    MissingDuringScan = 3,
    Binary = 4,
    Unsupported = 5,
    Error = 6,
}
