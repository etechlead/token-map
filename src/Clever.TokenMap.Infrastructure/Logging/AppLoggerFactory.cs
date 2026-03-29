using System.Globalization;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Core.Settings;
using Clever.TokenMap.Infrastructure.Settings;
using Serilog;
using Serilog.Events;

namespace Clever.TokenMap.Infrastructure.Logging;

public sealed class AppLoggerFactory : IAppLoggerFactory, IDisposable
{
    private const long DefaultFileSizeLimitBytes = 4 * 1024 * 1024;
    private const int DefaultRetainedFileCountLimit = 10;
    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}";
    private const string ConsoleOutputTemplate =
        "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}";

    private readonly Serilog.ILogger _logger;

    public AppLoggerFactory(
        LoggingSettings settings,
        string? logsDirectoryPath = null,
        IAppStoragePaths? appStoragePaths = null,
        long fileSizeLimitBytes = DefaultFileSizeLimitBytes,
        int retainedFileCountLimit = DefaultRetainedFileCountLimit,
        bool? enableConsoleSinkInDebugMode = null)
    {
        var storagePaths = appStoragePaths ?? new TokenMapAppDataPaths();
        var logDirectoryPath = string.IsNullOrWhiteSpace(logsDirectoryPath)
            ? storagePaths.GetLogsDirectoryPath()
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

        public void Log(AppLogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var serilogLevel = ToSerilogLevel(entry.Level);
            if (!_logger.IsEnabled(serilogLevel))
            {
                return;
            }

            var logger = _logger;
            if (!string.IsNullOrWhiteSpace(entry.EventCode))
            {
                logger = logger.ForContext("EventCode", entry.EventCode);
            }

            foreach (var (key, value) in entry.Context)
            {
                logger = logger.ForContext(key, value);
            }

            if (entry.Exception is null)
            {
                logger.Write(serilogLevel, "{Text:l}", entry.Message);
                return;
            }

            logger.Write(serilogLevel, entry.Exception, "{Text:l}", entry.Message);
        }
    }
}
