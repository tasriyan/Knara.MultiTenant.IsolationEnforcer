using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Multitenant.Enforcer.Caching;

public interface ICacheService
{
	Task InvalidateDomainCacheAsync(string domain, CancellationToken cancellationToken);
	Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken);
	Task<int> PrewarmCacheAsync(CancellationToken cancellationToken);
}

public class CacheService(
	ITenantLookupCache cache,
	ILogger<CacheService> logger,
	IOptions<MultiTenantOptions> options,
	ITenantDataProvider dataProvider) : ICacheService
{
	private readonly ITenantLookupCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly ILogger<CacheService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly MultiTenantOptions _options = options.Value;
	private readonly ITenantDataProvider _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));

	/// <summary>
	/// Pre-warms the cache with tenant information.
	/// </summary>
	/// <returns>Number of tenants cached</returns>
	public async Task<int> PrewarmCacheAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Pre-warming tenant cache...");

		var allTenants = await _dataProvider.GetAllActiveTenantsAsync(cancellationToken);
		int cachedCount = 0;

		foreach (var tenant in allTenants)
		{
			// Cache tenant info
			var tenantInfoCacheKey = new TenantInfoCacheKey(tenant.Id);
			var cacheOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
				Priority = CacheItemPriority.Normal
			};
			await _cache.SetAsync(tenantInfoCacheKey, tenant, cacheOptions, cancellationToken);

			// Cache domain mapping
			if (!string.IsNullOrEmpty(tenant.Domain))
			{
				var domainCacheKey = new TenantDomainCacheKey(tenant.Domain);
				await _cache.SetAsync(domainCacheKey, tenant.Id, cacheOptions, cancellationToken);
			}

			cachedCount++;
		}

		_logger.LogInformation("Pre-warmed cache with {Count} tenants", cachedCount);
		return cachedCount;
	}

	public async Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfoCacheKey = new TenantInfoCacheKey(tenantId);
		await _cache.RemoveAsync(tenantInfoCacheKey, cancellationToken);
		_logger.LogDebug("Invalidated cache for tenant {TenantId}", tenantId);
	}

	public async Task InvalidateDomainCacheAsync(string domain, CancellationToken cancellationToken)
	{
		var domainCacheKey = new TenantDomainCacheKey(domain);
		await _cache.RemoveAsync(domainCacheKey, cancellationToken);
		_logger.LogDebug("Invalidated cache for domain {Domain}", domain);
	}
}
