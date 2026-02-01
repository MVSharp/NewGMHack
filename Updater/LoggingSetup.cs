using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Updater;

/// <summary>
/// Configures ZLogger for the Updater application with console and file output.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Builds and configures an ILoggerFactory with ZLogger providers.
    /// </summary>
    /// <returns>Configured ILoggerFactory instance.</returns>
    public static ILoggerFactory BuildLoggerFactory()
    {
        var factory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);

            // Console output with Spectre.Console integration (plain text)
            logging.AddZLoggerConsole(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    // Format: "2025-02-02 12:34:56|INFO| Message"
                    formatter.SetPrefixFormatter($"{0}|{1}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));
                });
            });

            // File output to logs/updater_{date}_{sequence}.log
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequenceNumber) =>
                    $"logs/updater_{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log";
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 10240; // 10MB per file

                options.UsePlainTextFormatter(formatter =>
                {
                    // Format: "2025-02-02 12:34:56|INFO| Message"
                    formatter.SetPrefixFormatter($"{0}|{1}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));

                    // Exception formatter: "Exception message"
                    formatter.SetExceptionFormatter((writer, ex) =>
                        Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                });
            });
        });

        return factory;
    }
}
