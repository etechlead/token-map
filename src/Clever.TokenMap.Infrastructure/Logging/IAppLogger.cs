namespace Clever.TokenMap.Infrastructure.Logging;

public interface IAppLogger
{
    bool IsEnabled(AppLogLevel level);

    void Log(AppLogLevel level, string message, Exception? exception = null);
}
