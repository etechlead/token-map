using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Logging;

public interface IAppLogger
{
    void Log(AppLogEntry entry);
}
