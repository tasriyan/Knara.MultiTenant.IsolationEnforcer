using Microsoft.Extensions.Caching.Memory;

namespace Multitenant.Enforcer.Caching;

/// <summary>
/// Defines methods for managing tenant-specific data in a cache, including retrieval, storage, and removal of cached
/// items.
/// </summary>
/// <remarks>This interface provides asynchronous methods for interacting with a cache, allowing for efficient
/// storage and retrieval of tenant-related data. It supports operations such as retrieving cached items, adding or
/// updating items with optional expiration policies, removing specific items, and clearing the entire cache.</remarks>
public interface ITenantCache
{
	Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
	Task SetAsync<T>(string cacheKey, T data, TimeSpan? expiry, CancellationToken cancellationToken = default);
	Task SetAsync<T>(string cacheKey, T data, MemoryCacheEntryOptions options, CancellationToken cancellationToken = default);
	Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);
}
