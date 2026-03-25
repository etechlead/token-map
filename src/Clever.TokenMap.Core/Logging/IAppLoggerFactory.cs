namespace Clever.TokenMap.Core.Logging;

public interface IAppLoggerFactory
{
    IAppLogger CreateLogger<TCategory>();
}
