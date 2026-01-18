using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services.Caching;

/// <summary>
/// In-memory cache implementation using ConcurrentDictionary.
/// Used as fallback when SQLite cache is not available.
/// </summary>
public class InMemoryEntityCache<T> : IEntityCache<T> where T : class
{
    private readonly ConcurrentDictionary<uint, (T Entity, DateTime LastUpdatedAt)> _cache = new();

    public Task<T?> GetAsync(uint id)
    {
        if (_cache.TryGetValue(id, out var entry))
        {
            return Task.FromResult<T?>(entry.Entity);
        }
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync(uint id, T entity)
    {
        _cache[id] = (entity, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public Task<bool> IsValidAsync(uint id, TimeSpan maxAge)
    {
        if (_cache.TryGetValue(id, out var entry))
        {
            return Task.FromResult(DateTime.UtcNow - entry.LastUpdatedAt < maxAge);
        }
        return Task.FromResult(false);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public Task<T?> GetIfValidAsync(uint id, TimeSpan maxAge)
    {
        if (_cache.TryGetValue(id, out var entry))
        {
            if (DateTime.UtcNow - entry.LastUpdatedAt < maxAge)
            {
                return Task.FromResult<T?>(entry.Entity);
            }
        }
        return Task.FromResult<T?>(null);
    }

    public Task<IDictionary<uint, T>> GetManyIfValidAsync(IEnumerable<uint> ids, TimeSpan maxAge)
    {
        var result = new Dictionary<uint, T>();
        var now = DateTime.UtcNow;

        foreach (var id in ids)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                if (now - entry.LastUpdatedAt < maxAge)
                {
                    result[id] = entry.Entity;
                }
            }
        }

        return Task.FromResult<IDictionary<uint, T>>(result);
    }

    public Task SetManyAsync(IDictionary<uint, T> entities)
    {
        var now = DateTime.UtcNow;
        foreach (var check in entities)
        {
            _cache[check.Key] = (check.Value, now);
        }
        return Task.CompletedTask;
    }
}
