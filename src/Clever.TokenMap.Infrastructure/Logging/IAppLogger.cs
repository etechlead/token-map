namespace Clever.TokenMap.Infrastructure.Logging;

public interface IAppLogger
{
    void Log(AppLogLevel level, string message, Exception? exception = null);
}
