using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using MessagePack;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Services.Caching;

/// <summary>
/// SQLite-backed cache implementation with TTL support.
/// Stores entities as MessagePack blobs with LastUpdatedAt timestamp.
/// </summary>
public class SqliteEntityCache<T> : IEntityCache<T> where T : class
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly ILogger<SqliteEntityCache<T>> _logger;
    private bool _initialized;

    public SqliteEntityCache(ILogger<SqliteEntityCache<T>> logger)
    {
        _logger = logger;
        _tableName = $"{typeof(T).Name}Cache";
        
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NewGMHack");
        Directory.CreateDirectory(folder);
        _connectionString = $"Data Source={Path.Combine(folder, "rewards.db")}";
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            
            string createTableSql = $@"
                CREATE TABLE IF NOT EXISTS {_tableName} (
                    Id INTEGER PRIMARY KEY,
                    Data BLOB NOT NULL,
                    LastUpdatedAt TEXT NOT NULL
                )";
            await conn.ExecuteAsync(createTableSql);
            
            _logger.ZLogInformation($"SQLite cache table '{_tableName}' initialized");
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to initialize SQLite cache table '{_tableName}'");
            throw;
        }
    }

    public async Task<T?> GetAsync(uint id)
    {
        try
        {
            await EnsureInitializedAsync();

            await using var conn = new SqliteConnection(_connectionString);
            var row = await conn.QueryFirstOrDefaultAsync<CacheRow>(
                $"SELECT Id, Data, LastUpdatedAt FROM {_tableName} WHERE Id = @Id",
                new { Id = (long)id });
            
            if (row == null) return null;
            
            return MessagePackSerializer.Deserialize<T>(row.Data);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error reading from cache table '{_tableName}' for Id={id}");
            return null;
        }
    }

    public async Task SetAsync(uint id, T entity)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var data = MessagePackSerializer.Serialize(entity);
            var now = DateTime.UtcNow.ToString("O");

            await using var conn = new SqliteConnection(_connectionString);
            await conn.ExecuteAsync(
                $@"INSERT OR REPLACE INTO {_tableName} (Id, Data, LastUpdatedAt) 
                   VALUES (@Id, @Data, @LastUpdatedAt)",
                new { Id = (long)id, Data = data, LastUpdatedAt = now });
            
            _logger.ZLogInformation($"Cached {typeof(T).Name} Id={id} to SQLite");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error writing to cache table '{_tableName}' for Id={id}");
        }
    }

    public async Task<bool> IsValidAsync(uint id, TimeSpan maxAge)
    {
        try
        {
            await EnsureInitializedAsync();

            await using var conn = new SqliteConnection(_connectionString);
            var lastUpdated = await conn.QueryFirstOrDefaultAsync<string>(
                $"SELECT LastUpdatedAt FROM {_tableName} WHERE Id = @Id",
                new { Id = (long)id });
            
            if (string.IsNullOrEmpty(lastUpdated)) return false;
            
            if (DateTime.TryParse(lastUpdated, out var dt))
            {
                return DateTime.UtcNow - dt < maxAge;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error checking validity in cache table '{_tableName}' for Id={id}");
            return false;
        }
    }

    public void Clear()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Execute($"DELETE FROM {_tableName}");
            _logger.ZLogInformation($"Cleared SQLite cache table '{_tableName}'");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error clearing cache table '{_tableName}'");
        }
    }

    private class CacheRow
    {
        public long Id { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string LastUpdatedAt { get; set; } = "";
    }
}
