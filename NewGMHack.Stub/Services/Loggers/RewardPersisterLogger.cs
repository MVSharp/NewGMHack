using Microsoft.Extensions.Logging;
using ZLogger;
using System;

namespace NewGMHack.Stub.Services.Loggers;

public static partial class RewardPersisterLogger
{
    [ZLoggerMessage(LogLevel.Information, "RewardPersisterService started. DB: {connectionString}")]
    public static partial void LogServiceStarted(this ILogger logger, string connectionString);

    [ZLoggerMessage(LogLevel.Error, "Failed to init DB: {message}. Service disabled.")]
    public static partial void LogInitFailed(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Error, "Error in RewardPersisterService loop")]
    public static partial void LogLoopError(this ILogger logger, Exception ex);
    
    [ZLoggerMessage(LogLevel.Error, "Failed to flush reward record")]
    public static partial void LogFlushError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "Existing columns: {columns}")]
    public static partial void LogExistingColumns(this ILogger logger, string columns);

    [ZLoggerMessage(LogLevel.Information, "Adding missing column: {columnName}")]
    public static partial void LogAddingColumn(this ILogger logger, string columnName);

    [ZLoggerMessage(LogLevel.Information, "Successfully added column {columnName}")]
    public static partial void LogColumnAdded(this ILogger logger, string columnName);

    [ZLoggerMessage(LogLevel.Information, "Database initialized successfully")]
    public static partial void LogDbInitialized(this ILogger logger);

    [ZLoggerMessage(LogLevel.Error, "Failed to init DB")]
    public static partial void LogDbInitError(this ILogger logger, Exception ex);
}
