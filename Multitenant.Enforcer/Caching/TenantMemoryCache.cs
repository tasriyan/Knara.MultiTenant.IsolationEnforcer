using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace Multitenant.Enforcer.Caching;

/// <summary>
/// Provides an in-memory cache for tenant lookup operations.
/// </summary>
/// <remarks>This class is designed to store and retrieve tenant-related data using an in-memory
/// caching mechanism. It is suitable for testing or scenarios where a lightweight, non-persistent cache is sufficient.</remarks>
public class TenantMemoryCache(IMemoryCache memoryCache) : ITenantCache
{
	private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

	public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey, nameof(cacheKey));

		cancellationToken.ThrowIfCancellationRequested();

		var result = _memoryCache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is T value 
			? value 
			: default;

		return Task.FromResult(result);
	}

	public Task SetAsync<T>(string cacheKey, T data, TimeSpan? expiry, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey, nameof(cacheKey));

		cancellationToken.ThrowIfCancellationRequested();

		var options = new MemoryCacheEntryOptions();
		
		if (expiry.HasValue)
		{
			options.AbsoluteExpirationRelativeToNow = expiry.Value;
		}

		_memoryCache.Set(cacheKey, data, options);

		return Task.CompletedTask;
	}

	public Task SetAsync<T>(string cacheKey, T data, MemoryCacheEntryOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey, nameof(cacheKey));
		ArgumentNullException.ThrowIfNull(options);

		cancellationToken.ThrowIfCancellationRequested();

		_memoryCache.Set(cacheKey, data, options);

		return Task.CompletedTask;
	}

	public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey, nameof(cacheKey));

		cancellationToken.ThrowIfCancellationRequested();

		_memoryCache.Remove(cacheKey);

		return Task.CompletedTask;
	}
}
