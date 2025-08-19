using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Cache;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.DomainResolvers;

public interface ITenantLookupService
{
	Task<TenantInfo?> GetTenantInfoByDomainAsync(string domain, CancellationToken cancellationToken);
	Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken);
}

public class TenantLookupService(
	ITenantsCache tenantCache,
	ILogger<TenantLookupService> logger,
	IOptions<MultiTenantOptions> options,
	IReadOnlyTenants tenantStore) : ITenantLookupService
{
	private readonly ITenantsCache _tenantCache = tenantCache ?? throw new ArgumentNullException(nameof(tenantCache));
	private readonly ILogger<TenantLookupService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly MultiTenantOptions _options = options?.Value ?? MultiTenantOptions.DefaultOptions;
	private readonly IReadOnlyTenants _tenantStore = tenantStore ?? throw new ArgumentNullException(nameof(tenantStore));

	public async Task<TenantInfo?> GetTenantInfoByDomainAsync(string domain, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(domain))
			return null;

		var domainCacheKey = new TenantDomainCacheKey(domain);
		if (_options.CacheTenantResolution)
		{
			var cachedInfo = await _tenantCache.GetAsync<TenantInfo?>(domainCacheKey, cancellationToken);
			if (cachedInfo != null)
			{
				_logger.LogDebug("Cache hit for domain {Domain} -> tenant {TenantId}", domain, cachedInfo.Id);
				return cachedInfo;
			}
		}

		var tenantInfo = await _tenantStore.GetTenantInfoByDomainAsync(domain, cancellationToken: cancellationToken);
		if (tenantInfo != null && _options.CacheTenantResolution)
		{
			var cacheOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
				SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2),
				Priority = CacheItemPriority.High
			};

			await _tenantCache.SetAsync(domainCacheKey, tenantInfo, cacheOptions, cancellationToken);

			_logger.LogDebug("Cached tenant resolution: domain {Domain} -> tenant {TenantId}", domain, tenantInfo.Id);
		}
		else if (tenantInfo == null)
		{
			_logger.LogWarning("No tenant found for domain: {Domain}", domain);
		}

		return tenantInfo;
	}

	public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfoCacheKey = new TenantInfoCacheKey(tenantId);

		if (_options.CacheTenantResolution)
		{
			var cachedInfo = await _tenantCache.GetAsync<TenantInfo?>(tenantInfoCacheKey, cancellationToken);
			if (cachedInfo != null)
			{
				_logger.LogDebug("Cache hit for tenant info {TenantId}", tenantId);
				return cachedInfo;
			}
		}

		var tenantInfo = await _tenantStore.GetTenantInfoAsync(tenantId, cancellationToken);

		if (tenantInfo != null && _options.CacheTenantResolution)
		{
			var cacheOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
				Priority = CacheItemPriority.Normal
			};

			await _tenantCache.SetAsync(tenantInfoCacheKey, tenantInfo, cacheOptions, cancellationToken);
			_logger.LogDebug("Cached tenant info for {TenantId}", tenantId);
		}

		return tenantInfo;
	}
}
