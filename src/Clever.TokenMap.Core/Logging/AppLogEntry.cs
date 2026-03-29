using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Logging;

public sealed class AppLogEntry
{
    public required AppLogLevel Level { get; init; }

    public required string Message { get; init; }

    public string? EventCode { get; init; }

    public Exception? Exception { get; init; }

    public IReadOnlyDictionary<string, string> Context { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
