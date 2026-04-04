namespace Clever.TokenMap.Metrics.Derived;

public sealed record CallableHotspotThresholds(
    int LongCallableLines,
    int HighCyclomaticComplexity,
    int DeepNestingDepth,
    int LongParameterList)
{
    public static CallableHotspotThresholds Default { get; } =
        new(
            LongCallableLines: 30,
            HighCyclomaticComplexity: 10,
            DeepNestingDepth: 4,
            LongParameterList: 5);
}
