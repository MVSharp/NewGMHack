using Microsoft.Extensions.Logging;
using ZLogger;
using System;

namespace NewGMHack.Stub.Services.Loggers;

public static partial class GMMemoryLogger
{
    [ZLoggerMessage(LogLevel.Debug, "ScanMachine cache hit: {id}")]
    public static partial void LogScanMachineCacheHit(this ILogger logger, uint id);

    [ZLoggerMessage(LogLevel.Error, "Failed to cache Machine {id}")]
    public static partial void LogCacheMachineError(this ILogger logger, Exception ex, uint id);

    [ZLoggerMessage(LogLevel.Debug, "ScanSkill cache hit: {id}")]
    public static partial void LogScanSkillCacheHit(this ILogger logger, uint id);

    [ZLoggerMessage(LogLevel.Error, "Failed to cache Skill {id}")]
    public static partial void LogCacheSkillError(this ILogger logger, Exception ex, uint id);
    
    [ZLoggerMessage(LogLevel.Debug, "ScanWeapon cache hit: {id}")]
    public static partial void LogScanWeaponCacheHit(this ILogger logger, uint id);

    [ZLoggerMessage(LogLevel.Error, "Failed to cache Weapon {id}")]
    public static partial void LogCacheWeaponError(this ILogger logger, Exception ex, uint id);

    [ZLoggerMessage(LogLevel.Debug, "ScanMachineWithDetails Batch: {weaponCount} Weapons, {skillCount} Skills")]
    public static partial void LogScanMachineWithDetailsBatch(this ILogger logger, int weaponCount, int skillCount);

    [ZLoggerMessage(LogLevel.Debug, "Found weapon {id} at 0x{address:X}")]
    public static partial void LogFoundWeapon(this ILogger logger, uint id, long address);

    [ZLoggerMessage(LogLevel.Debug, "Found skill {id} at 0x{address:X}")]
    public static partial void LogFoundSkill(this ILogger logger, uint id, long address);

    [ZLoggerMessage(LogLevel.Debug, "ScanSkills Batch: {count} items")]
    public static partial void LogScanSkillsBatch(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Debug, "ScanWeapons Batch: {count} items")]
    public static partial void LogScanWeaponsBatch(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Debug, "ScanMachines Batch: {count} items")]
    public static partial void LogScanMachinesBatch(this ILogger logger, int count);

    [ZLoggerMessage(LogLevel.Debug, "Found machine {id} at 0x{address:X}")]
    public static partial void LogFoundMachine(this ILogger logger, uint id, long address);

    [ZLoggerMessage(LogLevel.Debug, "Scan{typeName} Input: {id}")]
    public static partial void LogScanGenericInput(this ILogger logger, string typeName, uint id);

    [ZLoggerMessage(LogLevel.Debug, "Process not found")]
    public static partial void LogScanGenericProcessNotFound(this ILogger logger);

    [ZLoggerMessage(LogLevel.Debug, "Scan{typeName} found {count} addresses in {elapsedMs}ms")]
    public static partial void LogScanGenericFoundAddresses(this ILogger logger, string typeName, int count, long elapsedMs);

    [ZLoggerMessage(LogLevel.Debug, "Not found {typeName}:{id}")]
    public static partial void LogScanGenericNotFound(this ILogger logger, string typeName, uint id);

    [ZLoggerMessage(LogLevel.Debug, "Found {typeName} at addr:0x{address:X}")]
    public static partial void LogScanGenericFoundAtAddress(this ILogger logger, string typeName, long address);

    [ZLoggerMessage(LogLevel.Debug, "Scan{typeName} Error: {message}")]
    public static partial void LogScanGenericError(this ILogger logger, string typeName, string message);

    [ZLoggerMessage(LogLevel.Debug, "[Machine] ID:{id} {filePath} Name:{chineseName}/{englishName} Rank:{rank} HP:{hp} Combat:{combatType} Skills:{skill1},{skill2}")]
    public static partial void LogMachineDebugInfo(this ILogger logger, uint id, string filePath, string chineseName, string englishName, string rank, uint hp, string combatType, uint skill1, uint skill2);

    [ZLoggerMessage(LogLevel.Debug, "[Skill] ID:{id} Name:{name} Movement:{movement} Atk:{attack} Def:{defense}")]
    public static partial void LogSkillDebugInfo(this ILogger logger, uint id, string name, int movement, int attack, int defense);

    [ZLoggerMessage(LogLevel.Debug, "[Weapon] ID:{id} Name:{name} Type:{type} Dmg:{damage} Range:{range} Ammo:{ammo}")]
    public static partial void LogWeaponDebugInfo(this ILogger logger, uint id, string name, string type, uint damage, float range, int ammo);
}
