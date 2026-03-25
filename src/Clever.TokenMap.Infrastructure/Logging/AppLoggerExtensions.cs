using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Infrastructure.Logging;

public static class AppLoggerExtensions
{
    public static void LogTrace(this IAppLogger logger, string message) =>
        logger.Log(AppLogLevel.Trace, message);

    public static void LogDebug(this IAppLogger logger, string message) =>
        logger.Log(AppLogLevel.Debug, message);

    public static void LogInformation(this IAppLogger logger, string message) =>
        logger.Log(AppLogLevel.Information, message);

    public static void LogWarning(this IAppLogger logger, Exception exception, string message) =>
        logger.Log(AppLogLevel.Warning, message, exception);

    public static void LogError(this IAppLogger logger, Exception exception, string message) =>
        logger.Log(AppLogLevel.Error, message, exception);
}
