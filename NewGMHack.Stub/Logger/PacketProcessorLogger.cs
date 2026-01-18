using System;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Logger;

public static partial class PacketProcessorLogger
{
    [ZLoggerMessage(LogLevel.Information, "Starting Parse for packet")]
    public static partial void LogStartingParse(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Finished Parse")]
    public static partial void LogFinishedParse(this ILogger logger);

    [ZLoggerMessage(LogLevel.Critical, "the packet processor stopped")]
    public static partial void LogPacketProcessorStopped(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Error occur in packet processor")]
    public static partial void LogPacketProcessorError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "page count:{count}: {machineId} exp: {currentExp} : battery {battery} slot:{slot}")]
    public static partial void LogPageCount(this ILogger logger, uint count, uint machineId, uint currentExp, float battery, uint slot);

    [ZLoggerMessage(LogLevel.Information, "Sending MachineInfoUpdate for machine {machineId}")]
    public static partial void LogSendingMachineInfoUpdate(this ILogger logger, uint machineId);

    [ZLoggerMessage(LogLevel.Error, "ScanCondom/IPC Error")]
    public static partial void LogScanCondomIPCError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Error in ReadPlayerBasicInfo")]
    public static partial void LogReadPlayerBasicInfoError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "PlayerBasicInfo: Id={playerId} Name={playerName} | Bot: Id={machineId} Slot={slot} Exp={currentExp} Bat={battery}")]
    public static partial void LogPlayerBasicInfo(this ILogger logger, uint playerId, string playerName, uint machineId, uint slot, uint currentExp, float battery);

    [ZLoggerMessage(LogLevel.Information, "Battle session ended: {sessionId}")]
    public static partial void LogBattleSessionEnded(this ILogger logger, string sessionId);

    [ZLoggerMessage(LogLevel.Information, "GameReady: MyPlayerId={playerId} Map={mapId} GameType={gameType} IsTeam={isTeam} PlayerCount={playerCount}")]
    public static partial void LogGameReady(this ILogger logger, uint playerId, uint mapId, uint gameType, uint isTeam, int playerCount);

    [ZLoggerMessage(LogLevel.Information, "Batch scanning {count} machines")]
    public static partial void LogBatchScanningMachines(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Information, "Batch scanning {count} transformed machines")]
    public static partial void LogBatchScanningTransformed(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Information, "Batch scanning {count} weapons")]
    public static partial void LogBatchScanningWeapons(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Error, "Error during batch scanning phase in ReadGameReady")]
    public static partial void LogBatchScanningError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "Player[{roomSlot}]: Id={playerId} Team={teamId1},{teamId2} Machine={machineId} HP={maxHP} Atk={attack} Def={defense} Shield={shield}")]
    public static partial void LogPlayerStats(this ILogger logger, int roomSlot, uint playerId, uint teamId1, uint teamId2, uint machineId, uint maxHP, int attack, int defense, uint shield);

    [ZLoggerMessage(LogLevel.Information, "Set current machine from GameReady: MachineId={machineId}")]
    public static partial void LogSetCurrentMachine(this ILogger logger, uint machineId);

    [ZLoggerMessage(LogLevel.Error, "ScanCondom error for self")]
    public static partial void LogScanCondomError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Error in ReadGameReady")]
    public static partial void LogReadGameReadyError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "battle reborn packet: MyPlayerId={myPlayerId} SpawnId={spawnId}")]
    public static partial void LogBattleReborn(this ILogger logger, uint myPlayerId, uint spawnId);

    [ZLoggerMessage(LogLevel.Error, "Error in ReadPlayerStats")]
    public static partial void LogReadPlayerStatsError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "change condom detected:{machineId}")]
    public static partial void LogCondomChangeDetected(this ILogger logger, uint machineId);

    [ZLoggerMessage(LogLevel.Information, "error in bomb history remove : readdead 1506")]
    public static partial void LogBombHistoryRemoveError(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "removed :{deadId} since it is dead from 1506")]
    public static partial void LogBombHistoryRemoved(this ILogger logger, uint deadId);

    [ZLoggerMessage(LogLevel.Error, "error occur in readdeads 1506")]
    public static partial void LogReadDeads1506Error(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "gift buffer: {buffer}")]
    public static partial void LogGiftBuffer(this ILogger logger, string buffer);

    [ZLoggerMessage(LogLevel.Information, "gifts count : {count}")]
    public static partial void LogGiftsCount(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Information, "[ReadDeads] PersonId={personId} KillerId={killerId} Count={count} BufferLen={bufferLen}")]
    public static partial void LogReadDeads(this ILogger logger, uint personId, uint killerId, uint count, int bufferLen);

    [ZLoggerMessage(LogLevel.Information, "[ReadDeads] cannot remove:{deadId}")]
    public static partial void LogReadDeadsCannotRemove(this ILogger logger, uint deadId);

    [ZLoggerMessage(LogLevel.Error, "[ReadDeads] error in readdead")]
    public static partial void LogReadDeadsErrorFromEx(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Error in hit response 2472")]
    public static partial void LogHitResponse2472Error(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Error in hit response 1616")]
    public static partial void LogHitResponse1616Error(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "Grade Player:{playerId} Grade:{grade} Damage:{damageScore} Team:{teamExpectationScore} Skill:{skillFulScore}")]
    public static partial void LogRewardGrade(this ILogger logger, uint playerId, string grade, uint damageScore, uint teamExpectationScore, uint skillFulScore);

    [ZLoggerMessage(LogLevel.Information, "Report Player[{gameStatus}]:{playerId} K:{kills} D:{deaths} S:{supports} Point:{points} Exp:{expGain} GB:{gbGain} MachineAddedExp:{machineAddedExp} MachineExp:{machineExp} Practice:{practiceExpAdded}")]
    public static partial void LogRewardReport(this ILogger logger, string gameStatus, uint playerId, uint kills, uint deaths, uint supports, uint points, long expGain, uint gbGain, uint machineAddedExp, uint machineExp, uint practiceExpAdded);
    
    [ZLoggerMessage(LogLevel.Information, "Bonus Player:{playerId} Values: {b0}|{b1}|{b2}|{b3}|{b4}|{b5}|{b6}|{b7}")]
    public static partial void LogRewardBonus(this ILogger logger, uint playerId, uint b0, uint b1, uint b2, uint b3, uint b4, uint b5, uint b6, uint b7);

    [ZLoggerMessage(LogLevel.Information, "room start index found")]
    public static partial void LogRoomStartIndexFound(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "room end index found")]
    public static partial void LogRoomEndIndexFound(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "target counts : {count} -- {targets}")]
    public static partial void LogTargetCounts(this ILogger logger, int count, string targets);

    [ZLoggerMessage(LogLevel.Error, "Error in SendToBombServices")]
    public static partial void LogSendToBombServicesError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Information, "charging condom: {slot}")]
    public static partial void LogChargingCondom(this ILogger logger, uint slot);

    [ZLoggerMessage(LogLevel.Information, "Machine id begin scan: {machineId}")]
    public static partial void LogMachineScanBegin(this ILogger logger, uint machineId);

    [ZLoggerMessage(LogLevel.Information, "Machine id scan completed: {machineId}")]
    public static partial void LogMachineScanCompleted(this ILogger logger, uint machineId);

    [ZLoggerMessage(LogLevel.Information, "Machine: {chineseName} | W1:{w1} | W2:{w2} | W3:{w3}")]
    public static partial void LogMachineInfo(this ILogger logger, string chineseName, uint w1, uint w2, uint w3);

    [ZLoggerMessage(LogLevel.Error, "Failed to send received message")]
    public static partial void LogSendReceivedMessageError(this ILogger logger, Exception ex);

    [ZLoggerMessage(LogLevel.Error, "Cannot destroy buildings: No active socket.")]
    public static partial void LogDestroyBuildingsNoSocket(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Starting DestroyAllBuildings (7000-8000)...")]
    public static partial void LogStartingDestroyBuildings(this ILogger logger);

    [ZLoggerMessage(LogLevel.Information, "Finished DestroyAllBuildings.")]
    public static partial void LogFinishedDestroyBuildings(this ILogger logger);

    [ZLoggerMessage(LogLevel.Error, "Error sending building damage packet")]
    public static partial void LogSendBuildingDamageError(this ILogger logger, Exception ex);
}
