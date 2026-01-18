using System;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using ZLogger;

namespace NewGMHack.Stub.Logger;

public static partial class DirectInputLogicLogger
{
    [ZLoggerMessage(LogLevel.Information, "null pointer on data")]
    public static partial void LogNullDataPointer(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "our things: All features disabled, cleared scheduled events")]
    public static partial void LogAllFeaturesDisabled(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Unknown :{deviceType} | {size}")]
    public static partial void LogUnknownDevice(this ILogger logger, DeviceType deviceType, int size);

    [ZLoggerMessage(LogLevel.Information, "handle auto ready")]
    public static partial void LogHandleAutoReady(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "handle auto aim")]
    public static partial void LogHandleAimSupport(this ILogger logger);
}
