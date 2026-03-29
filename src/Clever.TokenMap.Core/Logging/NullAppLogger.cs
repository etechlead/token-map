namespace Clever.TokenMap.Core.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Log(AppLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
    }
}
