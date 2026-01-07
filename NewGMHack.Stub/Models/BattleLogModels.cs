using System;
using System.Collections.Concurrent;

namespace NewGMHack.Stub.Models;

#region In-Memory Battle State (Thread-Safe)

/// <summary>
/// Current battle state - thread-safe for real-time access by OverlayManager/frontend
/// </summary>
public class BattleState
{
    private readonly object _lock = new();
    
    /// <summary>
    /// Current session ID (null if not in battle)
    /// </summary>
    public string? CurrentSessionId { get; private set; }
    
    /// <summary>
    /// Session start time
    /// </summary>
    public DateTime? SessionStartedAt { get; private set; }
    
    /// <summary>
    /// Map ID for current battle
    /// </summary>
    public byte MapId { get; private set; }
    
    /// <summary>
    /// Game type for current battle
    /// </summary>
    public byte GameType { get; private set; }
    
    /// <summary>
    /// Is team mode (vs random)
    /// </summary>
    public byte IsTeam { get; private set; }
    
    /// <summary>
    /// Per-player state for HP tracking - thread-safe
    /// </summary>
    public ConcurrentDictionary<uint, PlayerBattleState> Players { get; } = new();
    
    /// <summary>
    /// Start a new battle session
    /// </summary>
    public void StartSession(byte mapId, byte gameType, byte isTeam)
    {
        lock (_lock)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N");
            SessionStartedAt = DateTime.UtcNow;
            MapId = mapId;
            GameType = gameType;
            IsTeam = isTeam;
            Players.Clear();
        }
    }
    
    /// <summary>
    /// End current session
    /// </summary>
    public void EndSession()
    {
        lock (_lock)
        {
            CurrentSessionId = null;
            SessionStartedAt = null;
            Players.Clear();
        }
    }
    
    /// <summary>
    /// Add or update player state
    /// </summary>
    public void SetPlayer(uint playerId, uint teamId, uint machineId, uint maxHP, ushort attack, ushort defense, uint shield, uint roomSlot)
    {
        Players[playerId] = new PlayerBattleState
        {
            PlayerId = playerId,
            TeamId = teamId,
            MachineId = machineId,
            MaxHP = maxHP,
            CurrentHP = maxHP, // Start at full HP
            Attack = attack,
            Defense = defense,
            Shield = shield,
            CurrentShield = shield,
            RoomSlot = roomSlot
        };
    }
    
    /// <summary>
    /// Get player state by EntityId (RoomSlot + 1)
    /// </summary>
    public PlayerBattleState? GetPlayerByEntityId(uint entityId)
    {
        return Players.Values.FirstOrDefault(p => p.EntityId == entityId);
    }
    
    /// <summary>
    /// Update player HP after hit (returns HP change: positive=damage, negative=healing)
    /// </summary>
    public int UpdatePlayerHP(uint playerId, uint newHP, uint newShield)
    {
        if (Players.TryGetValue(playerId, out var state))
        {
            int hpDelta = (int)state.CurrentHP - (int)newHP; // positive = damage, negative = healing
            state.CurrentHP = newHP;
            state.CurrentShield = newShield;
            return hpDelta;
        }
        return 0;
    }
    
    /// <summary>
    /// Reset player HP (on reborn)
    /// </summary>
    public void ResetPlayerHP(uint playerId)
    {
        if (Players.TryGetValue(playerId, out var state))
        {
            state.CurrentHP = state.MaxHP;
            state.CurrentShield = state.Shield;
        }
    }
    /// <summary>
    /// Update player SP (from HitResponse)
    /// </summary>
    public void UpdatePlayerSP(uint playerId, uint sp)
    {
        if (Players.TryGetValue(playerId, out var state))
        {
            state.SP = sp;
        }
    }
}

/// <summary>
/// Individual player state during battle
/// </summary>
public class PlayerBattleState
{
    public uint PlayerId { get; set; }
    public uint TeamId { get; set; }
    public uint MachineId { get; set; }
    public uint MaxHP { get; set; }
    public uint CurrentHP { get; set; }
    public ushort Attack { get; set; }
    public ushort Defense { get; set; }
    public uint Shield { get; set; }
    public uint CurrentShield { get; set; }
    public uint SP { get; set; } // Special Points
    public uint RoomSlot { get; set; }
    public uint EntityId => RoomSlot + 1;
}

#endregion

#region SQLite Records

/// <summary>
/// Battle session record for SQLite
/// </summary>
public class BattleSessionRecord
{
    public string SessionId { get; set; } = "";
    public uint PlayerId { get; set; }
    public int MapId { get; set; }
    public int GameType { get; set; }
    public int IsTeam { get; set; }
    public int PlayerCount { get; set; }
    public string StartedAt { get; set; } = "";
    public string? EndedAt { get; set; }
}

/// <summary>
/// Battle player record for SQLite
/// </summary>
public class BattlePlayerRecord
{
    public string SessionId { get; set; } = "";
    public uint PlayerId { get; set; }
    public uint TeamId { get; set; }
    public uint MachineId { get; set; }
    public uint MaxHP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public uint Shield { get; set; }
}

/// <summary>
/// Damage event record for SQLite (positive=damage, negative=healing)
/// </summary>
public class DamageEventRecord
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public uint AttackerId { get; set; }
    public uint WeaponId { get; set; }
    public uint VictimId { get; set; }
    public int Damage { get; set; } // positive = damage, negative = healing
    public uint VictimHPAfter { get; set; }
    public uint VictimShieldAfter { get; set; }
    public int IsKill { get; set; }
}

/// <summary>
/// Death event record for SQLite
/// </summary>
public class DeathEventRecord
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public uint VictimId { get; set; }
    public uint KillerId { get; set; }
}

#endregion

#region Channel Events

/// <summary>
/// Event types for battle logging channel
/// </summary>
public enum BattleEventType
{
    SessionStart,
    PlayerJoin,
    Damage,
    Death,
    Reborn,
    SessionEnd
}

/// <summary>
/// Battle event for channel transport
/// </summary>
public class BattleLogEvent
{
    public BattleEventType Type { get; set; }
    public string SessionId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    
    // Session data
    public BattleSessionRecord? Session { get; set; }
    public List<BattlePlayerRecord>? Players { get; set; }
    
    // Damage data
    public DamageEventRecord? Damage { get; set; }
    
    // Death data
    public DeathEventRecord? Death { get; set; }
    
    // Reborn data
    public uint RebornPlayerId { get; set; }
}

#endregion
