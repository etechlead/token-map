namespace Clever.TokenMap.Infrastructure.Logging;

public sealed class NullAppLoggerFactory : IAppLoggerFactory
{
    public static NullAppLoggerFactory Instance { get; } = new();

    private NullAppLoggerFactory()
    {
    }

    public IAppLogger CreateLogger<TCategory>() => NullAppLogger.Instance;

    public IAppLogger CreateLogger(string categoryName) => NullAppLogger.Instance;
}
