using Microsoft.Extensions.Caching.Memory;

namespace Multitenant.Enforcer.Caching;

/// <summary>
/// Provides an in-memory cache for tenant lookup operations.
/// </summary>
/// <remarks>This class is designed to store and retrieve tenant-related data using an in-memory
/// caching mechanism. It is suitable for testing or scenarios where a lightweight, non-persistent cache is sufficient.
public class TenantLookupMemoryCache : ITenantLookupCache
{
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	public Task SetAsync<T>(string cacheKey, T data, TimeSpan? expiry, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	public Task SetAsync<T>(string cacheKey, T data, MemoryCacheEntryOptions options, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}
}
