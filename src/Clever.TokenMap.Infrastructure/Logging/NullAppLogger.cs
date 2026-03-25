using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Infrastructure.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Log(AppLogLevel level, string message, Exception? exception = null)
    {
    }
}
