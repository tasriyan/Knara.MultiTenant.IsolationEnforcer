using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Cache;

public interface ITenantCacheManager
{
	Task InvalidateDomainCacheAsync(string domain, CancellationToken cancellationToken);
	Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken);
	Task<int> PrewarmCacheAsync(CancellationToken cancellationToken);
}

public class TenantCacheManager(
				ILogger<TenantCacheManager> logger,
				ITenantCache tenantCache,
				ITenantStore tenantStore,
				IOptions<MemoryCacheEntryOptions> options) : ITenantCacheManager
{
	private readonly ITenantCache _tenantCache = tenantCache ?? throw new ArgumentNullException(nameof(tenantCache));


	/// <summary>
	/// Pre-warms the cache with tenant information.
	/// </summary>
	/// <returns>Number of tenants cached</returns>
	public async Task<int> PrewarmCacheAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Pre-warming tenant cache...");

		var allTenants = await tenantStore.GetAllActiveTenantsAsync(cancellationToken);
		int cachedCount = 0;
		var memoryCacheOptions = options?.Value ?? new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Default cache expiration
		};

		foreach (var tenant in allTenants)
		{
			// Cache tenant info
			var tenantInfoCacheKey = new TenantInfoCacheKey(tenant.Id);
			await _tenantCache.SetAsync(tenantInfoCacheKey, tenant, memoryCacheOptions, cancellationToken);

			// Cache domain mapping
			if (!string.IsNullOrEmpty(tenant.Domain))
			{
				var domainCacheKey = new TenantDomainCacheKey(tenant.Domain);
				await _tenantCache.SetAsync(domainCacheKey, tenant.Id, memoryCacheOptions, cancellationToken);
			}

			cachedCount++;
		}

		logger.LogInformation("Pre-warmed cache with {Count} tenants", cachedCount);
		return cachedCount;
	}

	public async Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfoCacheKey = new TenantInfoCacheKey(tenantId);
		await _tenantCache.RemoveAsync(tenantInfoCacheKey, cancellationToken);
		logger.LogDebug("Invalidated cache for tenant {TenantId}", tenantId);
	}

	public async Task InvalidateDomainCacheAsync(string domain, CancellationToken cancellationToken)
	{
		var domainCacheKey = new TenantDomainCacheKey(domain);
		await _tenantCache.RemoveAsync(domainCacheKey, cancellationToken);
		logger.LogDebug("Invalidated cache for domain {Domain}", domain);
	}
}
