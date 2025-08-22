using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Cache;
using Multitenant.Enforcer.Core;

namespace MultiTenant.Enforcer.Tests.Caching;

public class TenantCacheManagerTests
{
    private readonly Mock<ITenantsCache> _mockCache;
    private readonly Mock<ILogger<TenantCacheManager>> _mockLogger;
    private readonly Mock<ITenantStore> _mockDataProvider;
    private readonly MemoryCacheEntryOptions _options;
    private readonly TenantCacheManager _cacheManager;

    public TenantCacheManagerTests()
    {
        _mockCache = new Mock<ITenantsCache>();
        _mockLogger = new Mock<ILogger<TenantCacheManager>>();
        _mockDataProvider = new Mock<ITenantStore>();
        _options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
        
        var optionsWrapper = Options.Create(_options);
        _cacheManager = new TenantCacheManager(_mockLogger.Object, _mockCache.Object, _mockDataProvider.Object,  optionsWrapper);
    }

    [Fact]
    public async Task PrewarmCacheAsync_WithActiveTenants_CachesAllTenantsAndReturnsCount()
    {
        // Arrange
        var tenants = new[]
        {
            new TenantInfo { Id = Guid.NewGuid(), Domain = "tenant1.com", IsActive = true },
            new TenantInfo { Id = Guid.NewGuid(), Domain = "tenant2.com", IsActive = true },
            new TenantInfo { Id = Guid.NewGuid(), Domain = "", IsActive = true } // No domain
        };

        _mockDataProvider.Setup(x => x.GetAllActiveTenantsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(tenants);

        // Act
        var result = await _cacheManager.PrewarmCacheAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(3);

        // Verify tenant info was cached for all tenants
        _mockCache.Verify(x => x.SetAsync(
            new TenantInfoCacheKey(tenants[0].Id),
			tenants[0],
            It.Is<MemoryCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(15)),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockCache.Verify(x => x.SetAsync(
			new TenantInfoCacheKey(tenants[1].Id),
			tenants[1],
            It.IsAny<MemoryCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockCache.Verify(x => x.SetAsync(
			new TenantInfoCacheKey(tenants[2].Id),
			tenants[2],
            It.IsAny<MemoryCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify domain mappings were cached only for tenants with domains
        _mockCache.Verify(x => x.SetAsync(
            new TenantDomainCacheKey("tenant1.com"),
			tenants[0].Id,
            It.IsAny<MemoryCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockCache.Verify(x => x.SetAsync(
			new TenantDomainCacheKey("tenant2.com"),
			tenants[1].Id,
            It.IsAny<MemoryCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrewarmCacheAsync_WithNoTenants_ReturnsZero()
    {
        // Arrange
        _mockDataProvider.Setup(x => x.GetAllActiveTenantsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Array.Empty<TenantInfo>());

        // Act
        var result = await _cacheManager.PrewarmCacheAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        _mockCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<MemoryCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrewarmCacheAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        _mockDataProvider.Setup(x => x.GetAllActiveTenantsAsync(cancellationToken))
                        .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _cacheManager.PrewarmCacheAsync(cancellationToken));
    }

    [Fact]
    public async Task InvalidateTenantCacheAsync_RemovesTenantFromCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        await _cacheManager.InvalidateTenantCacheAsync(tenantId, CancellationToken.None);

        // Assert
        _mockCache.Verify(x => x.RemoveAsync(
			new TenantInfoCacheKey(tenantId),
			It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateDomainCacheAsync_RemovesDomainFromCache()
    {
        // Arrange
        var domain = "test.com";

        // Act
        await _cacheManager.InvalidateDomainCacheAsync(domain, CancellationToken.None);

        // Assert
        _mockCache.Verify(x => x.RemoveAsync(
			new TenantDomainCacheKey(domain),
			It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrewarmCacheAsync_UsesCorrectCacheOptions()
    {
        // Arrange
        var tenant = new TenantInfo { Id = Guid.NewGuid(), Domain = "test.com", IsActive = true };
        _mockDataProvider.Setup(x => x.GetAllActiveTenantsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync([tenant]);

        // Act
        await _cacheManager.PrewarmCacheAsync(CancellationToken.None);

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.Is<MemoryCacheEntryOptions>(o => 
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(15) &&
                o.Priority == CacheItemPriority.Normal),
            It.IsAny<CancellationToken>()), Times.Exactly(2)); // Once for tenant info, once for domain mapping
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(60)]
    public async Task PrewarmCacheAsync_UsesCacheExpirationFromOptions(int expirationMinutes)
    {
        // Arrange
        _options.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expirationMinutes);
        var tenant = new TenantInfo { Id = Guid.NewGuid(), Domain = "test.com", IsActive = true };
        _mockDataProvider.Setup(x => x.GetAllActiveTenantsAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { tenant });

        // Act
        await _cacheManager.PrewarmCacheAsync(CancellationToken.None);

        // Assert
        _mockCache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.Is<MemoryCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(expirationMinutes)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}