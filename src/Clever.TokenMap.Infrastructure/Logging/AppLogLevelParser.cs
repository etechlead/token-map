namespace Clever.TokenMap.Infrastructure.Logging;

public static class AppLogLevelParser
{
    public static AppLogLevel GetDefault()
    {
#if DEBUG
        return AppLogLevel.Debug;
#else
        return AppLogLevel.Warning;
#endif
    }

    public static AppLogLevel ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetDefault();
        }

        if (Enum.TryParse<AppLogLevel>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return GetDefault();
    }
}
