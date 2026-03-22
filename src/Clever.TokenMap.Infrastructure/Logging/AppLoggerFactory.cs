using System.Globalization;
using Clever.TokenMap.Infrastructure.Settings;
using Serilog;
using Serilog.Events;

namespace Clever.TokenMap.Infrastructure.Logging;

public sealed class AppLoggerFactory : IAppLoggerFactory, IDisposable
{
    private const long DefaultFileSizeLimitBytes = 4 * 1024 * 1024;
    private const int DefaultRetainedFileCountLimit = 10;
    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
    private const string ConsoleOutputTemplate =
        "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private readonly Serilog.ILogger _logger;

    public AppLoggerFactory(
        LoggingSettings settings,
        string? logsDirectoryPath = null,
        long fileSizeLimitBytes = DefaultFileSizeLimitBytes,
        int retainedFileCountLimit = DefaultRetainedFileCountLimit,
        bool? enableConsoleSinkInDebugMode = null)
    {
        var logDirectoryPath = string.IsNullOrWhiteSpace(logsDirectoryPath)
            ? TokenMapAppDataPaths.GetLogsDirectoryPath()
            : logsDirectoryPath;

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(ToSerilogLevel(settings.MinLevel))
            .WriteTo.File(
                path: Path.Combine(logDirectoryPath, "tokenmap-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: fileSizeLimitBytes,
                retainedFileCountLimit: retainedFileCountLimit,
                outputTemplate: FileOutputTemplate);

#if DEBUG
        if (enableConsoleSinkInDebugMode ?? true)
        {
            configuration = configuration.WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: ConsoleOutputTemplate);
        }
#endif

        _logger = configuration.CreateLogger();
    }

    public IAppLogger CreateLogger<TCategory>() =>
        CreateLoggerCore(typeof(TCategory).FullName ?? typeof(TCategory).Name);

    public void Dispose()
    {
        (_logger as IDisposable)?.Dispose();
    }

    private static LogEventLevel ToSerilogLevel(AppLogLevel level) =>
        level switch
        {
            AppLogLevel.Trace => LogEventLevel.Verbose,
            AppLogLevel.Debug => LogEventLevel.Debug,
            AppLogLevel.Information => LogEventLevel.Information,
            AppLogLevel.Warning => LogEventLevel.Warning,
            AppLogLevel.Error => LogEventLevel.Error,
            AppLogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };

    private AppLogger CreateLoggerCore(string categoryName) =>
        new AppLogger(_logger.ForContext("SourceContext", categoryName));

    private sealed class AppLogger : IAppLogger
    {
        private readonly Serilog.ILogger _logger;

        public AppLogger(Serilog.ILogger logger)
        {
            _logger = logger;
        }

        public void Log(AppLogLevel level, string message, Exception? exception = null)
        {
            var serilogLevel = ToSerilogLevel(level);
            if (!_logger.IsEnabled(serilogLevel))
            {
                return;
            }

            if (exception is null)
            {
                _logger.Write(serilogLevel, "{Text:l}", message);
                return;
            }

            _logger.Write(serilogLevel, exception, "{Text:l}", message);
        }
    }
}
