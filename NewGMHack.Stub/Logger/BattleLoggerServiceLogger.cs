using System;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Logger;

public static partial class BattleLoggerServiceLogger
{
    [ZLoggerMessage(LogLevel.Information, "BattleLoggerService started. DB: {connectionString}")]
    public static partial void LogServiceStarted(this ILogger logger, string connectionString);

    [ZLoggerMessage(LogLevel.Error, "Failed to init BattleLogger DB: {message}. Service disabled.")]
    public static partial void LogInitFailed(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Error, "Error in BattleLoggerService loop")]
    public static partial void LogLoopError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "Flushed {count} damage events")]
    public static partial void LogFlushedDamageEvents(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Error, "Error flushing damage buffer")]
    public static partial void LogFlushError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "Battle session started: {sessionId} Map={mapId} Players={playerCount}")]
    public static partial void LogSessionStarted(this ILogger logger, string sessionId, int mapId, int? playerCount);

    [ZLoggerMessage(LogLevel.Information, "Death recorded: Victim={victimId} Killer={killerId}")]
    public static partial void LogDeathRecorded(this ILogger logger, uint? victimId, uint? killerId);

    [ZLoggerMessage(LogLevel.Information, "Reborn recorded: Player={playerId}")]
    public static partial void LogRebornRecorded(this ILogger logger, uint playerId);

    [ZLoggerMessage(LogLevel.Information, "Battle session ended: {sessionId}")]
    public static partial void LogSessionEnded(this ILogger logger, string sessionId);

    [ZLoggerMessage(LogLevel.Information, "Added SessionId column to MatchRewards")]
    public static partial void LogAddedSessionIdColumn(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Battle logging tables initialized")]
    public static partial void LogTablesInitialized(this ILogger logger);
}
