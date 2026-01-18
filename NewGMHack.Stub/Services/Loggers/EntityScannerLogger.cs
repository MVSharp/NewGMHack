using Microsoft.Extensions.Logging;
using ZLogger;
using System;

namespace NewGMHack.Stub.Services.Loggers;

public static partial class EntityScannerLogger
{
    [ZLoggerMessage(LogLevel.Information, "EntityScannerService started.")]
    public static partial void LogServiceStarted(this ILogger logger);

    [ZLoggerMessage(LogLevel.Warning, "Target process not found. Service stopping.")]
    public static partial void LogProcessNotFound(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Attached to process: {processName} (PID: {pid})")]
    public static partial void LogAttached(this ILogger logger, string processName, int pid);

    [ZLoggerMessage(LogLevel.Error, "Unexpected error in scan loop.")]
    public static partial void LogScanLoopError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "EntityScannerService stopped.")]
    public static partial void LogServiceStopped(this ILogger logger);

    [ZLoggerMessage(LogLevel.Error, "ScanEntities failed.")]
    public static partial void LogScanEntitiesFailed(this ILogger logger, Exception ex);
}
