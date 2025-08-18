using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer;

public interface ITenantLookupService
{
	Task<Guid?> GetTenantIdByDomainAsync(string domain, CancellationToken cancellationToken);
	Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken);
}

public class TenantLookupService(
	ITenantCache cache,
	ILogger<TenantLookupService> logger,
	IOptions<MultiTenantOptions> options,
	ITenantDataProvider dataProvider) : ITenantLookupService
{
	private readonly ITenantCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
	private readonly ILogger<TenantLookupService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly MultiTenantOptions _options = options?.Value ?? MultiTenantOptions.DefaultOptions;
	private readonly ITenantDataProvider _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));

	public async Task<Guid?> GetTenantIdByDomainAsync(string domain, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(domain))
			return null;

		var domainCacheKey = new TenantDomainCacheKey(domain);
		if (_options.CacheTenantResolution)
		{
			var cachedTenantId = await _cache.GetAsync<Guid?>(domainCacheKey, cancellationToken);
			if (cachedTenantId.HasValue)
			{
				_logger.LogDebug("Cache hit for domain {Domain} -> tenant {TenantId}", domain, cachedTenantId);
				return cachedTenantId;
			}
		}

		var tenantId = await _dataProvider.GetActiveTenantIdByDomainAsync(domain, cancellationToken: cancellationToken);
		if (!tenantId.IsNullOrEmpty() && _options.CacheTenantResolution)
		{
			var cacheOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
				SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2),
				Priority = CacheItemPriority.High
			};

			await _cache.SetAsync(domainCacheKey, tenantId.Value, cacheOptions, cancellationToken);

			_logger.LogDebug("Cached tenant resolution: domain {Domain} -> tenant {TenantId}", domain, tenantId);
		}
		else if (!tenantId.HasValue)
		{
			_logger.LogWarning("No tenant found for domain: {Domain}", domain);
		}

		return tenantId;
	}

	public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfoCacheKey = new TenantInfoCacheKey(tenantId);

		if (_options.CacheTenantResolution)
		{
			var cachedInfo = await _cache.GetAsync<TenantInfo?>(tenantInfoCacheKey, cancellationToken);
			if (cachedInfo != null)
			{
				_logger.LogDebug("Cache hit for tenant info {TenantId}", tenantId);
				return cachedInfo;
			}
		}

		var tenantInfo = await _dataProvider.GetActiveTenantInfoAsync(tenantId, cancellationToken);

		if (tenantInfo != null && _options.CacheTenantResolution)
		{
			var cacheOptions = new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
				Priority = CacheItemPriority.Normal
			};

			await _cache.SetAsync(tenantInfoCacheKey, tenantInfo, cacheOptions, cancellationToken);
			_logger.LogDebug("Cached tenant info for {TenantId}", tenantId);
		}

		return tenantInfo;
	}
}
