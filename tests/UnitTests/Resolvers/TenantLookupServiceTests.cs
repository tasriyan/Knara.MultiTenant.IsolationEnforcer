using Knara.MultiTenant.IsolationEnforcer.Cache;
using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnitTests.DomainResolvers;

public class TenantLookupServiceTests
{
	private readonly Mock<ITenantsCache> _mockTenantCache;
	private readonly Mock<ILogger<TenantLookupService>> _mockLogger;
	private readonly Mock<IOptions<MultiTenantOptions>> _mockOptions;
	private readonly Mock<ITenantStore> _mockTenantStore;
	private readonly MultiTenantOptions _defaultOptions;
	private readonly TenantLookupService _service;

	public TenantLookupServiceTests()
	{
		_mockTenantCache = new Mock<ITenantsCache>();
		_mockLogger = new Mock<ILogger<TenantLookupService>>();
		_mockOptions = new Mock<IOptions<MultiTenantOptions>>();
		_mockTenantStore = new Mock<ITenantStore>();
		_defaultOptions = new MultiTenantOptions
		{
			CacheTenantResolution = true,
			CacheExpirationMinutes = 15
		};

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_service = new TenantLookupService(_mockTenantCache.Object, _mockLogger.Object, _mockOptions.Object, _mockTenantStore.Object);
	}

	#region GetTenantInfoByDomainAsync Tests

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithNullDomain_ReturnsNull()
	{
		// Act
		var result = await _service.GetTenantInfoByDomainAsync(null, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithEmptyDomain_ReturnsNull()
	{
		// Act
		var result = await _service.GetTenantInfoByDomainAsync("", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithWhitespaceDomain_ReturnsNull()
	{
		// Act
		var result = await _service.GetTenantInfoByDomainAsync("   ", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithCacheHit_ReturnsCachedResult()
	{
		// Arrange
		var domain = "acme";
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
		var cacheKey = new TenantDomainCacheKey(domain);

		string ck = cacheKey;
		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(ck, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoByDomainAsync(domain, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantStore.Verify(x => x.GetTenantInfoByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithCacheMiss_QueriesStore()
	{
		// Arrange
		var domain = "globex";
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoByDomainAsync(domain, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoByDomainAsync(domain, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantStore.Verify(x => x.GetTenantInfoByDomainAsync(domain, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithSuccessfulStore_CachesTenant()
	{
		// Arrange
		var domain = "initech";
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoByDomainAsync(domain, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		await _service.GetTenantInfoByDomainAsync(domain, CancellationToken.None);

		// Assert
		_mockTenantCache.Verify(x => x.SetAsync(
			It.IsAny<string>(),
			tenantInfo,
			It.IsAny<MemoryCacheEntryOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithCachingDisabled_DoesNotQueryCache()
	{
		// Arrange
		_defaultOptions.CacheTenantResolution = false;
		var domain = "nocache";
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };

		_mockTenantStore.Setup(x => x.GetTenantInfoByDomainAsync(domain, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoByDomainAsync(domain, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantCache.Verify(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		_mockTenantCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TenantInfo>(), It.IsAny<MemoryCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_WithNullStoreResult_ReturnsNull()
	{
		// Arrange
		var domain = "notfound";

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoByDomainAsync(domain, It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act
		var result = await _service.GetTenantInfoByDomainAsync(domain, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetTenantInfoByDomainAsync_PassesCancellationTokenToStore()
	{
		// Arrange
		var domain = "test";
		var cancellationToken = new CancellationTokenSource().Token;

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act
		await _service.GetTenantInfoByDomainAsync(domain, cancellationToken);

		// Assert
		_mockTenantStore.Verify(x => x.GetTenantInfoByDomainAsync(domain, cancellationToken), Times.Once);
	}

	#endregion

	#region GetTenantInfoAsync Tests

	[Fact]
	public async Task GetTenantInfoAsync_WithCacheHit_ReturnsCachedResult()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		var cacheKey = new TenantInfoCacheKey(tenantId);

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(cacheKey.ToString(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoAsync(tenantId, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantStore.Verify(x => x.GetTenantInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetTenantInfoAsync_WithCacheMiss_QueriesStore()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoAsync(tenantId, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantStore.Verify(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetTenantInfoAsync_WithSuccessfulStore_CachesTenant()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		await _service.GetTenantInfoAsync(tenantId, CancellationToken.None);

		// Assert
		_mockTenantCache.Verify(x => x.SetAsync(
			It.IsAny<string>(),
			tenantInfo,
			It.IsAny<MemoryCacheEntryOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetTenantInfoAsync_WithCachingDisabled_DoesNotQueryCache()
	{
		// Arrange
		_defaultOptions.CacheTenantResolution = false;
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantStore.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _service.GetTenantInfoAsync(tenantId, CancellationToken.None);

		// Assert
		result.ShouldBe(tenantInfo);
		_mockTenantCache.Verify(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		_mockTenantCache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<TenantInfo>(), It.IsAny<MemoryCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetTenantInfoAsync_WithNullStoreResult_ReturnsNull()
	{
		// Arrange
		var tenantId = Guid.NewGuid();

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);
		_mockTenantStore.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act
		var result = await _service.GetTenantInfoAsync(tenantId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetTenantInfoAsync_PassesCancellationTokenToStore()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var cancellationToken = new CancellationTokenSource().Token;

		_mockTenantCache.Setup(x => x.GetAsync<TenantInfo?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act
		await _service.GetTenantInfoAsync(tenantId, cancellationToken);

		// Assert
		_mockTenantStore.Verify(x => x.GetTenantInfoAsync(tenantId, cancellationToken), Times.Once);
	}

	#endregion
}
