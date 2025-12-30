using System.Data;
using System.Threading.Channels;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.PacketStructs.Recv;
using NewGMHack.CommunicationModel.IPC.Responses;
using NewGMHack.Stub.Models;
using ZLogger;

namespace NewGMHack.Stub.Services;

public class RewardPersisterService : BackgroundService
{
    private readonly Channel<RewardEvent> _channel;
    private readonly ILogger<RewardPersisterService> _logger;
    private readonly IpcNotificationService _ipcService;
    private readonly string _connectionString;
    
    private MatchRewardRecord? _pendingRecord;
    private DateTime _pendingSince;
    private const int FlushTimeoutMs = 2000;

    public RewardPersisterService(Channel<RewardEvent> channel, ILogger<RewardPersisterService> logger, IpcNotificationService ipcService)
    {
        _channel = channel;
        _logger = logger;
        _ipcService = ipcService;
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
            _logger.ZLogInformation($"RewardPersisterService started. DB: {_connectionString}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Failed to init DB: {ex.Message}. Service disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var waitTask = _channel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                var delayTask = _pendingRecord != null 
                    ? Task.Delay(FlushTimeoutMs, stoppingToken) 
                    : Task.Delay(-1, stoppingToken);

                var completedTask = await Task.WhenAny(waitTask, delayTask);

                if (completedTask == waitTask)
                {
                    if (await waitTask) // Data available
                    {
                        while (_channel.Reader.TryRead(out var evt))
                        {
                            await HandleEvent(evt);
                        }
                    }
                    else
                    {
                        break; // Channel closed
                    }
                }
                else
                {
                    // Timeout -> Flush
                    await FlushPending();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RewardPersisterService loop");
            }
        }
        
        await FlushPending();
    }

    private async Task HandleEvent(RewardEvent evt)
    {
        if (_pendingRecord == null)
        {
            StartNewRecord(evt);
            return;
        }

        // Check if this event belongs to the pending record (same player? close in time?)
        // If we already have the type of data this event provides, flush and start new.
        bool isDuplicateType = (evt.Report != null && _pendingRecord.Points != null) ||
                               (evt.Bonus != null && _pendingRecord.Bonus1 != null) ||
                               (evt.Grade != null && _pendingRecord.GradeRank != null);

        if (isDuplicateType || evt.Timestamp - _pendingSince > TimeSpan.FromSeconds(5))
        {
             await FlushPending();
             StartNewRecord(evt);
        }
        else
        {
            Merge(evt);
            
            // If full (has Report, Bonus, and Grade), flush immediately
            if (IsFull(_pendingRecord))
            {
                await FlushPending();
            }
        }
    }

    private void StartNewRecord(RewardEvent evt)
    {
        _pendingRecord = new MatchRewardRecord
        {
            CreatedAtUtc = evt.Timestamp.ToString("O"), // ISO 8601
            PlayerId = evt.Report?.PlayerId ?? evt.Bonus?.PlayerId ?? evt.Grade?.PlayerId ?? 0
        };
        _pendingSince = evt.Timestamp;
        Merge(evt);
    }

    private void Merge(RewardEvent evt)
    {
        if (_pendingRecord == null) return;

        if (evt.Report.HasValue)
        {
            var r = evt.Report.Value;
            _pendingRecord.PlayerId = r.PlayerId;
            
            // Transform WinOrLostOrDraw to GameStatus string
            var gameStatus = RewardTransformations.ToGameStatus(r.WinOrLostOrDraw);
            _pendingRecord.GameStatus = RewardTransformations.GameStatusToString(gameStatus);
            
            _pendingRecord.Kills = r.Kills;
            _pendingRecord.Deaths = r.Deaths;
            _pendingRecord.Supports = r.Supports;
            _pendingRecord.Points = r.Points;
            _pendingRecord.ExpGain = r.ExpGain;
            _pendingRecord.GBGain = r.GBGain;
            _pendingRecord.MachineAddedExp = r.MachineAddedExp;
            _pendingRecord.MachineExp = r.MachineExp;
            _pendingRecord.PracticeExpAdded = r.PracticeExpAdded;
        }

        if (evt.Bonus != null)
        {
            var b = evt.Bonus;
            _pendingRecord.PlayerId = b.PlayerId;
            _pendingRecord.Bonus1 = b.Bonuses[0];
            _pendingRecord.Bonus2 = b.Bonuses[1];
            _pendingRecord.Bonus3 = b.Bonuses[2];
            _pendingRecord.Bonus4 = b.Bonuses[3];
            _pendingRecord.Bonus5 = b.Bonuses[4];
            _pendingRecord.Bonus6 = b.Bonuses[5];
            _pendingRecord.Bonus7 = b.Bonuses[6];
            _pendingRecord.Bonus8 = b.Bonuses[7];
        }

        if (evt.Grade != null)
        {
            var g = evt.Grade;
            _pendingRecord.PlayerId = g.PlayerId;
            _pendingRecord.GradeRank = RewardTransformations.GradeRankToString(g.Grade);
            _pendingRecord.DamageScore = g.DamageScore;
            _pendingRecord.TeamExpectationScore = g.TeamExpectationScore;
            _pendingRecord.SkillFulScore = g.SkillFulScore;
        }
    }

    private bool IsFull(MatchRewardRecord record)
    {
        // Has Report data (checked via Points), Bonus data (checked via Bonus1), and Grade data
        return record.Points.HasValue && record.Bonus1.HasValue && record.GradeRank != null;
    }

    private async Task FlushPending()
    {
        if (_pendingRecord == null) return;
        
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            string sql = @"
                INSERT INTO MatchRewards (
                    PlayerId, CreatedAtUtc, GameStatus,
                    Kills, Deaths, Supports, Points, ExpGain, GBGain, MachineAddedExp, MachineExp, PracticeExpAdded,
                    GradeRank, DamageScore, TeamExpectationScore, SkillFulScore,
                    Bonus1, Bonus2, Bonus3, Bonus4, Bonus5, Bonus6, Bonus7, Bonus8
                ) VALUES (
                    @PlayerId, @CreatedAtUtc, @GameStatus,
                    @Kills, @Deaths, @Supports, @Points, @ExpGain, @GBGain, @MachineAddedExp, @MachineExp, @PracticeExpAdded,
                    @GradeRank, @DamageScore, @TeamExpectationScore, @SkillFulScore,
                    @Bonus1, @Bonus2, @Bonus3, @Bonus4, @Bonus5, @Bonus6, @Bonus7, @Bonus8
                )";
            
            await conn.ExecuteAsync(sql, _pendingRecord);
            _logger.LogInformation($"Saved reward record for Player {_pendingRecord.PlayerId} [{_pendingRecord.GameStatus}] Grade:{_pendingRecord.GradeRank}");

            // Send Notification
            var notification = new RewardNotification
            {
                RecordId = _pendingRecord.Id,
                PlayerId = _pendingRecord.PlayerId,
                Points = _pendingRecord.Points ?? 0,
                Kills = _pendingRecord.Kills ?? 0,
                Deaths = _pendingRecord.Deaths ?? 0,
                Supports = _pendingRecord.Supports ?? 0,
                HasBonus = _pendingRecord.Bonus1.HasValue,
                Timestamp = _pendingRecord.CreatedAtUtc
            };
            await _ipcService.SendRewardNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush reward record");
        }
        finally
        {
            _pendingRecord = null;
        }
    }

    private async Task InitializeDb()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            
            // Step 1: Create table if not exists (for fresh installs)
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS MatchRewards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlayerId INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    Kills INTEGER,
                    Deaths INTEGER,
                    Supports INTEGER,
                    Points INTEGER,
                    ExpGain INTEGER,
                    GBGain INTEGER,
                    MachineAddedExp INTEGER,
                    PracticeExpAdded INTEGER,
                    Bonus1 INTEGER,
                    Bonus2 INTEGER,
                    Bonus3 INTEGER,
                    Bonus4 INTEGER,
                    Bonus5 INTEGER,
                    Bonus6 INTEGER,
                    Bonus7 INTEGER,
                    Bonus8 INTEGER
                )";
            await conn.ExecuteAsync(createTableSql);
            
            // Step 2: Add new columns if they don't exist (migration for existing tables)
            var existingColumns = (await conn.QueryAsync<string>(
                "SELECT name FROM pragma_table_info('MatchRewards')")).ToHashSet();
            
            _logger.ZLogInformation($"Existing columns: {string.Join(", ", existingColumns)}");
            
            var newColumns = new Dictionary<string, string>
            {
                ["GameStatus"] = "TEXT",
                ["MachineExp"] = "INTEGER",
                ["GradeRank"] = "TEXT",
                ["DamageScore"] = "INTEGER",
                ["TeamExpectationScore"] = "INTEGER",
                ["SkillFulScore"] = "INTEGER"
            };

            foreach (var (columnName, columnType) in newColumns)
            {
                if (!existingColumns.Contains(columnName))
                {
                    _logger.ZLogInformation($"Adding missing column: {columnName}");
                    await conn.ExecuteAsync($"ALTER TABLE MatchRewards ADD COLUMN {columnName} {columnType}");
                    _logger.ZLogInformation($"Successfully added column {columnName}");
                }
            }
            
            // Step 3: Create indexes AFTER columns exist
            await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_PlayerId ON MatchRewards(PlayerId)");
            await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_CreatedAt ON MatchRewards(CreatedAtUtc)");
            await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_GameStatus ON MatchRewards(GameStatus)");
            
            _logger.ZLogInformation($"Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to init DB");
            throw;
        }
    }
}
