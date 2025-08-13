// MultiTenant.Enforcer.AspNetCore/CachedTenantLookupService.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using MultiTenant.Enforcer.Core;

namespace MultiTenant.Enforcer.Core
{
    /// <summary>
    /// Cached implementation of tenant lookup service for performance.
    /// </summary>
    public class CachedTenantLookupService : ITenantLookupService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedTenantLookupService> _logger;
        private readonly MultiTenantOptions _options;
        private readonly ITenantDataProvider _dataProvider;

        public CachedTenantLookupService(
            IMemoryCache cache,
            ILogger<CachedTenantLookupService> logger,
            IOptions<MultiTenantOptions> options,
            ITenantDataProvider dataProvider)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value;
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        }

        public async Task<Guid?> GetTenantIdByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            var cacheKey = $"tenant_domain_{domain.ToLowerInvariant()}";

            if (_options.CacheTenantResolution && _cache.TryGetValue(cacheKey, out Guid cachedTenantId))
            {
                _logger.LogDebug("Cache hit for domain {Domain} -> tenant {TenantId}", domain, cachedTenantId);
                return cachedTenantId;
            }

            var tenantId = await _dataProvider.GetTenantIdByDomainAsync(domain);

            if (tenantId.HasValue && _options.CacheTenantResolution)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes / 2),
                    Priority = CacheItemPriority.High
                };

                _cache.Set(cacheKey, tenantId.Value, cacheOptions);

                _logger.LogDebug("Cached tenant resolution: domain {Domain} -> tenant {TenantId}", domain, tenantId);
            }
            else if (!tenantId.HasValue)
            {
                _logger.LogWarning("No tenant found for domain: {Domain}", domain);
            }

            return tenantId;
        }

        public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId)
        {
            var cacheKey = $"tenant_info_{tenantId}";

            if (_options.CacheTenantResolution && _cache.TryGetValue(cacheKey, out TenantInfo? cachedInfo))
            {
                _logger.LogDebug("Cache hit for tenant info {TenantId}", tenantId);
                return cachedInfo;
            }

            var tenantInfo = await _dataProvider.GetTenantInfoAsync(tenantId);

            if (tenantInfo != null && _options.CacheTenantResolution)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, tenantInfo, cacheOptions);
                _logger.LogDebug("Cached tenant info for {TenantId}", tenantId);
            }

            return tenantInfo;
        }

        /// <summary>
        /// Invalidates cached tenant information.
        /// </summary>
        /// <param name="tenantId">The tenant ID to invalidate</param>
        public void InvalidateTenantCache(Guid tenantId)
        {
            _cache.Remove($"tenant_info_{tenantId}");
            _logger.LogInformation("Invalidated cache for tenant {TenantId}", tenantId);
        }

        /// <summary>
        /// Invalidates cached domain mapping.
        /// </summary>
        /// <param name="domain">The domain to invalidate</param>
        public void InvalidateDomainCache(string domain)
        {
            var cacheKey = $"tenant_domain_{domain.ToLowerInvariant()}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Invalidated cache for domain {Domain}", domain);
        }

        /// <summary>
        /// Pre-warms the cache with tenant information.
        /// </summary>
        /// <returns>Number of tenants cached</returns>
        public async Task<int> PrewarmCacheAsync()
        {
            _logger.LogInformation("Pre-warming tenant cache...");

            var allTenants = await _dataProvider.GetAllActiveTenantsAsync();
            int cachedCount = 0;

            foreach (var tenant in allTenants)
            {
                // Cache tenant info
                var tenantInfoCacheKey = $"tenant_info_{tenant.Id}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };
                _cache.Set(tenantInfoCacheKey, tenant, cacheOptions);

                // Cache domain mapping
                if (!string.IsNullOrEmpty(tenant.Domain))
                {
                    var domainCacheKey = $"tenant_domain_{tenant.Domain.ToLowerInvariant()}";
                    _cache.Set(domainCacheKey, tenant.Id, cacheOptions);
                }

                cachedCount++;
            }

            _logger.LogInformation("Pre-warmed cache with {Count} tenants", cachedCount);
            return cachedCount;
        }
    }

    /// <summary>
    /// Interface for accessing tenant data from storage.
    /// </summary>
    public interface ITenantDataProvider
    {
        /// <summary>
        /// Gets tenant ID by domain name.
        /// </summary>
        Task<Guid?> GetTenantIdByDomainAsync(string domain);

        /// <summary>
        /// Gets tenant information by ID.
        /// </summary>
        Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId);

        /// <summary>
        /// Gets all active tenants for cache pre-warming.
        /// </summary>
        Task<TenantInfo[]> GetAllActiveTenantsAsync();
    }

    /// <summary>
    /// Entity Framework implementation of tenant data provider.
    /// </summary>
    public class EntityFrameworkTenantDataProvider : ITenantDataProvider
    {
        private readonly DbContext _context;
        private readonly ILogger<EntityFrameworkTenantDataProvider> _logger;

        public EntityFrameworkTenantDataProvider(
            DbContext context,
            ILogger<EntityFrameworkTenantDataProvider> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid?> GetTenantIdByDomainAsync(string domain)
        {
            try
            {
                // This assumes you have a Tenants/Companies table with Domain and Id columns
                // Adjust the query based on your actual entity structure
                var query = _context.Set<TenantEntity>()
                    .Where(t => t.Domain == domain && t.IsActive)
                    .Select(t => t.Id);

                return await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tenant ID for domain {Domain}", domain);
                return null;
            }
        }

        public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId)
        {
            try
            {
                var tenant = await _context.Set<TenantEntity>()
                    .Where(t => t.Id == tenantId && t.IsActive)
                    .Select(t => new TenantInfo
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Domain = t.Domain,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                return tenant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tenant info for {TenantId}", tenantId);
                return null;
            }
        }

        public async Task<TenantInfo[]> GetAllActiveTenantsAsync()
        {
            try
            {
                var tenants = await _context.Set<TenantEntity>()
                    .Where(t => t.IsActive)
                    .Select(t => new TenantInfo
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Domain = t.Domain,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt
                    })
                    .ToArrayAsync();

                return tenants;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all active tenants");
                return Array.Empty<TenantInfo>();
            }
        }
    }

    /// <summary>
    /// In-memory implementation for testing or simple scenarios.
    /// </summary>
    public class InMemoryTenantDataProvider : ITenantDataProvider
    {
        private readonly TenantInfo[] _tenants;

        public InMemoryTenantDataProvider(TenantInfo[] tenants)
        {
            _tenants = tenants ?? Array.Empty<TenantInfo>();
        }

        public Task<Guid?> GetTenantIdByDomainAsync(string domain)
        {
            var tenant = _tenants.FirstOrDefault(t => 
                t.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && t.IsActive);
            
            return Task.FromResult(tenant?.Id);
        }

        public Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId)
        {
            var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId && t.IsActive);
            return Task.FromResult(tenant);
        }

        public Task<TenantInfo[]> GetAllActiveTenantsAsync()
        {
            var activeTenants = _tenants.Where(t => t.IsActive).ToArray();
            return Task.FromResult(activeTenants);
        }
    }

    /// <summary>
    /// Entity class representing tenant data in the database.
    /// This should match your actual tenant/company entity structure.
    /// </summary>
    public class TenantEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
