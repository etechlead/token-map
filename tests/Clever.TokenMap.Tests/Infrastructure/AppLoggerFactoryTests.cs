using Clever.TokenMap.Core.Logging;
using Clever.TokenMap.Infrastructure.Logging;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.Tests.Infrastructure;

public sealed class AppLoggerFactoryTests : IDisposable
{
    private readonly string _testRootPath = Path.Combine(
        Path.GetTempPath(),
        "TokenMap.Logging.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Logger_WritesEntriesToRollingFile()
    {
        string logFilePath;

        using (var factory = CreateFactory(AppLogLevel.Debug))
        {
            factory.CreateLogger<TestsLoggingCategory>().LogInformation("Analysis started.");
        }

        var logFiles = Directory.GetFiles(_testRootPath, "tokenmap-*.log");
        logFilePath = Assert.Single(logFiles);
        var content = File.ReadAllText(logFilePath);

        Assert.Contains("TestsLoggingCategory: Analysis started.", content);
    }

    [Fact]
    public void Logger_DoesNotWriteEntriesBelowMinimumLevel()
    {
        using var factory = CreateFactory(AppLogLevel.Warning);

        factory.CreateLogger<TestsLoggingCategory>().LogInformation("This should be filtered.");

        Assert.False(Directory.Exists(_testRootPath));
    }

    [Fact]
    public void Logger_RollsWhenFileSizeLimitIsReached()
    {
        using var factory = CreateFactory(
            AppLogLevel.Information,
            fileSizeLimitBytes: 512,
            retainedFileCountLimit: 10);
        var logger = factory.CreateLogger<TestsLoggingCategory>();

        for (var index = 0; index < 40; index++)
        {
            logger.LogInformation($"entry-{index:D2} {new string('x', 120)}");
        }

        Assert.True(Directory.GetFiles(_testRootPath, "tokenmap-*.log").Length > 1);
    }

    [Fact]
    public void Logger_RetainsOnlyConfiguredNumberOfFiles()
    {
        using var factory = CreateFactory(
            AppLogLevel.Information,
            fileSizeLimitBytes: 256,
            retainedFileCountLimit: 2);
        var logger = factory.CreateLogger<TestsLoggingCategory>();

        for (var index = 0; index < 60; index++)
        {
            logger.LogInformation($"retain-{index:D2} {new string('y', 120)}");
        }

        Assert.True(Directory.GetFiles(_testRootPath, "tokenmap-*.log").Length <= 2);
    }

#if DEBUG
    [Fact]
    public void Logger_WritesToConsoleInDebugMode()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            using var factory = CreateFactory(AppLogLevel.Information, enableConsoleSinkInDebugMode: true);
            factory.CreateLogger<TestsLoggingCategory>().LogInformation("console-check");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("console-check", writer.ToString());
    }
#endif

    public void Dispose()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    private AppLoggerFactory CreateFactory(
        AppLogLevel minLevel,
        long fileSizeLimitBytes = 4 * 1024 * 1024,
        int retainedFileCountLimit = 10,
        bool enableConsoleSinkInDebugMode = false) =>
        new(
            new LoggingSettings
            {
                MinLevel = minLevel,
            },
            logsDirectoryPath: _testRootPath,
            fileSizeLimitBytes: fileSizeLimitBytes,
            retainedFileCountLimit: retainedFileCountLimit,
            enableConsoleSinkInDebugMode: enableConsoleSinkInDebugMode);

    private sealed class TestsLoggingCategory;
}
