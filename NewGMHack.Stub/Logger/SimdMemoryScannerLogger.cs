using System;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Logger;

public static partial class SimdMemoryScannerLogger
{
    [ZLoggerMessage(LogLevel.Warning, "ScanJob Pattern {patternId} error: {message}")]
    public static partial void LogScanJobPatternError(this ILogger logger, int patternId, string message);

    [ZLoggerMessage(LogLevel.Error, "Batch Scan Job Error at 0x{baseAddress:X}: {message}")]
    public static partial void LogBatchScanJobError(this ILogger logger, long baseAddress, string message);
}
