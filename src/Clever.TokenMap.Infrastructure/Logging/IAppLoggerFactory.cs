namespace Clever.TokenMap.Infrastructure.Logging;

public interface IAppLoggerFactory
{
    IAppLogger CreateLogger<TCategory>();

    IAppLogger CreateLogger(string categoryName);
}
