using System;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Logger;

public static partial class WinsockHookLogger
{
    [ZLoggerMessage(LogLevel.Information, "Starting hook service...")]
    public static partial void LogStartingHookService(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Container path : {path}")]
    public static partial void LogContainerPath(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Information, "Failed. Error: {errorCode}")]
    public static partial void LogContainerPathFailed(this ILogger logger, int errorCode);

    [ZLoggerMessage(LogLevel.Information, "Begin Hook: {managerName}")]
    public static partial void LogBeginHook(this ILogger logger, string managerName);

    [ZLoggerMessage(LogLevel.Information, "Hooked: {managerName}")]
    public static partial void LogHooked(this ILogger logger, string managerName);

    [ZLoggerMessage(LogLevel.Error, "Failed to hook {managerName}: {message} | {stackTrace}")]
    public static partial void LogHookFailed(this ILogger logger, string managerName, string message, string? stackTrace);

    [ZLoggerMessage(LogLevel.Information, "Stopping hook service...")]
    public static partial void LogStoppingHookService(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Unhooked: {managerName}")]
    public static partial void LogUnhooked(this ILogger logger, string managerName);

    [ZLoggerMessage(LogLevel.Error, "Failed to unhook {managerName}: {message}")]
    public static partial void LogUnhookFailed(this ILogger logger, string managerName, string message);
}
