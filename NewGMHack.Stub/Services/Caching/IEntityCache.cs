using System;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services.Caching;

/// <summary>
/// Generic cache interface for entity storage with TTL support.
/// </summary>
/// <typeparam name="T">Entity type (must be a class)</typeparam>
public interface IEntityCache<T> where T : class
{
    /// <summary>
    /// Get entity from cache. Returns null if not found or expired.
    /// </summary>
    Task<T?> GetAsync(uint id);
    
    /// <summary>
    /// Store entity in cache with current timestamp.
    /// </summary>
    Task SetAsync(uint id, T entity);
    
    /// <summary>
    /// Check if entry exists and is not expired (older than maxAge).
    /// </summary>
    Task<bool> IsValidAsync(uint id, TimeSpan maxAge);
    
    /// <summary>
    /// Clear all cached entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Get entity if it exists and is valid in a single operation.
    /// </summary>
    Task<T?> GetIfValidAsync(uint id, TimeSpan maxAge);

    /// <summary>
    /// Batch retrieve valid entities.
    /// </summary>
    Task<IDictionary<uint, T>> GetManyIfValidAsync(IEnumerable<uint> ids, TimeSpan maxAge);

    /// <summary>
    /// Batch store entities.
    /// </summary>
    Task SetManyAsync(IDictionary<uint, T> entities);
}
