using Microsoft.Extensions.Caching.Memory;

namespace Multitenant.Enforcer.Caching;

public interface ITenantCache
{
	Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
	Task SetAsync<T>(string cacheKey, T data, TimeSpan? expiry, CancellationToken cancellationToken = default);
	Task SetAsync<T>(string cacheKey, T data, MemoryCacheEntryOptions options, CancellationToken cancellationToken = default);
	Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);
}
