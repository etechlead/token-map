using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Enums;

namespace Clever.TokenMap.Core.Logging;

public static class AppLoggerExtensions
{
    public static void LogTrace(
        this IAppLogger logger,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Trace, message, eventCode, context));

    public static void LogDebug(
        this IAppLogger logger,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Debug, message, eventCode, context));

    public static void LogInformation(
        this IAppLogger logger,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Information, message, eventCode, context));

    public static void LogWarning(
        this IAppLogger logger,
        Exception exception,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Warning, message, eventCode, context, exception));

    public static void LogError(
        this IAppLogger logger,
        Exception exception,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Error, message, eventCode, context, exception));

    public static void LogCritical(
        this IAppLogger logger,
        Exception exception,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null) =>
        logger.Log(CreateEntry(AppLogLevel.Critical, message, eventCode, context, exception));

    public static AppLogEntry CreateEntry(
        AppLogLevel level,
        string message,
        string? eventCode = null,
        IReadOnlyDictionary<string, string>? context = null,
        Exception? exception = null) =>
        new()
        {
            Level = level,
            Message = message,
            EventCode = eventCode,
            Context = context ?? AppIssueContext.Empty,
            Exception = exception,
        };
}
