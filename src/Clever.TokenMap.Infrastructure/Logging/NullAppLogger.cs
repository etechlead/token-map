namespace Clever.TokenMap.Infrastructure.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public bool IsEnabled(AppLogLevel level) => false;

    public void Log(AppLogLevel level, string message, Exception? exception = null)
    {
    }
}
