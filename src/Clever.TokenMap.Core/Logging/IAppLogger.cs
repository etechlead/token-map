using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Logging;

public interface IAppLogger
{
    void Log(AppLogLevel level, string message, Exception? exception = null);
}
