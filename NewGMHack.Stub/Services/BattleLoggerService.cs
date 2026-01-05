using System.Threading.Channels;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.Models;
using ZLogger;

namespace NewGMHack.Stub.Services;

/// <summary>
/// Background service that persists battle events to SQLite.
/// Receives events via Channel for async processing.
/// </summary>
public class BattleLoggerService : BackgroundService
{
    private readonly Channel<BattleLogEvent> _channel;
    private readonly ILogger<BattleLoggerService> _logger;
    private readonly string _connectionString;
    private bool _initialized;

    public BattleLoggerService(
        Channel<BattleLogEvent> channel,
        ILogger<BattleLoggerService> logger)
    {
        _channel = channel;
        _logger = logger;
        
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NewGMHack");
        Directory.CreateDirectory(folder);
        _connectionString = $"Data Source={Path.Combine(folder, "rewards.db")}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken); // Wait for hooks to settle

        try
        {
            await InitializeDb();
            _logger.ZLogInformation($"BattleLoggerService started. DB: {_connectionString}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Failed to init BattleLogger DB: {ex.Message}. Service disabled.");
            return;
        }

        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessEvent(evt);
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Error processing battle event: {evt.Type}");
            }
        }
    }

    private async Task ProcessEvent(BattleLogEvent evt)
    {
        switch (evt.Type)
        {
            case BattleEventType.SessionStart:
                await InsertSession(evt.Session!);
                if (evt.Players != null)
                {
                    foreach (var player in evt.Players)
                    {
                        await InsertPlayer(player);
                    }
                }
                break;
                
            case BattleEventType.Damage:
                await InsertDamage(evt.Damage!);
                break;
                
            case BattleEventType.Death:
                await InsertDeath(evt.Death!);
                break;
                
            case BattleEventType.SessionEnd:
                await UpdateSessionEnd(evt.SessionId, evt.Timestamp);
                break;
        }
    }

    private async Task InsertSession(BattleSessionRecord session)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO BattleSessions (SessionId, PlayerId, MapId, GameType, IsTeam, PlayerCount, StartedAt)
            VALUES (@SessionId, @PlayerId, @MapId, @GameType, @IsTeam, @PlayerCount, @StartedAt)",
            session);
        _logger.ZLogInformation($"Battle session started: {session.SessionId} Map={session.MapId} Players={session.PlayerCount}");
    }

    private async Task InsertPlayer(BattlePlayerRecord player)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO BattlePlayers (SessionId, PlayerId, TeamId, MachineId, MaxHP, Attack, Defense, Shield)
            VALUES (@SessionId, @PlayerId, @TeamId, @MachineId, @MaxHP, @Attack, @Defense, @Shield)",
            player);
    }

    private async Task InsertDamage(DamageEventRecord damage)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO DamageEvents (SessionId, Timestamp, AttackerId, WeaponId, VictimId, Damage, VictimHPAfter, VictimShieldAfter, IsKill)
            VALUES (@SessionId, @Timestamp, @AttackerId, @WeaponId, @VictimId, @Damage, @VictimHPAfter, @VictimShieldAfter, @IsKill)",
            damage);
    }

    private async Task InsertDeath(DeathEventRecord death)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO DeathEvents (SessionId, Timestamp, VictimId, KillerId)
            VALUES (@SessionId, @Timestamp, @VictimId, @KillerId)",
            death);
        _logger.ZLogInformation($"Death recorded: Victim={death.VictimId} Killer={death.KillerId}");
    }

    private async Task UpdateSessionEnd(string sessionId, DateTime endedAt)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            UPDATE BattleSessions SET EndedAt = @EndedAt WHERE SessionId = @SessionId",
            new { SessionId = sessionId, EndedAt = endedAt.ToString("O") });
        _logger.ZLogInformation($"Battle session ended: {sessionId}");
    }

    private async Task InitializeDb()
    {
        if (_initialized) return;
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        // Create BattleSessions table
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS BattleSessions (
                SessionId TEXT PRIMARY KEY,
                PlayerId INTEGER NOT NULL,
                MapId INTEGER,
                GameType INTEGER,
                IsTeam INTEGER,
                PlayerCount INTEGER,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT
            )");

        // Create BattlePlayers table
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS BattlePlayers (
                SessionId TEXT NOT NULL,
                PlayerId INTEGER NOT NULL,
                TeamId INTEGER,
                MachineId INTEGER,
                MaxHP INTEGER,
                Attack INTEGER,
                Defense INTEGER,
                Shield INTEGER,
                PRIMARY KEY (SessionId, PlayerId)
            )");

        // Create DamageEvents table
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS DamageEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                AttackerId INTEGER,
                WeaponId INTEGER,
                VictimId INTEGER,
                Damage INTEGER,
                VictimHPAfter INTEGER,
                VictimShieldAfter INTEGER,
                IsKill INTEGER
            )");

        // Create DeathEvents table
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS DeathEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                VictimId INTEGER,
                KillerId INTEGER
            )");

        // Add SessionId column to MatchRewards if not exists
        var existingColumns = (await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('MatchRewards')")).ToHashSet();
        
        if (!existingColumns.Contains("SessionId"))
        {
            await conn.ExecuteAsync("ALTER TABLE MatchRewards ADD COLUMN SessionId TEXT");
            _logger.ZLogInformation($"Added SessionId column to MatchRewards");
        }

        // Create indexes
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_DamageEvents_Session ON DamageEvents(SessionId)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_DeathEvents_Session ON DeathEvents(SessionId)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_BattlePlayers_Session ON BattlePlayers(SessionId)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_MatchRewards_Session ON MatchRewards(SessionId)");

        _initialized = true;
        _logger.ZLogInformation($"Battle logging tables initialized");
    }
}
