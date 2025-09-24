using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace UnitTests.Resolvers;

public class SubdomainTenantResolverTests
{
	private readonly Mock<ILogger<SubdomainTenantResolver>> _mockLogger;
	private readonly Mock<ITenantLookupService> _mockTenantLookupService;
	private readonly Mock<IOptions<SubdomainTenantResolverOptions>> _mockOptions;
	private readonly SubdomainTenantResolverOptions _defaultOptions;
	private readonly SubdomainTenantResolver _resolver;

	public SubdomainTenantResolverTests()
	{
		_mockLogger = new Mock<ILogger<SubdomainTenantResolver>>();
		_mockTenantLookupService = new Mock<ITenantLookupService>();
		_mockOptions = new Mock<IOptions<SubdomainTenantResolverOptions>>();
		_defaultOptions = new SubdomainTenantResolverOptions
		{
			ExcludedSubdomains = ["www", "admin", "api"],
			SystemAdminClaimTypes = ["role"],
			SystemAdminClaimValue = "SystemAdmin"
		};

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_resolver = new SubdomainTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithValidSubdomain_ReturnsCorrectTenant()
	{
		// Arrange
		var context = CreateHttpContext("acme.example.com");
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("acme", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.IsSystemContext.ShouldBeFalse();
		result.ContextSource.ShouldBe("Subdomain:acme");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithExcludedSubdomain_UsesSecondPart()
	{
		// Arrange
		var context = CreateHttpContext("www.globex.example.com");
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("globex", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Subdomain:globex");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithNoSubdomain_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext("example.com");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Could not extract tenant from request");
		exception.AttemptedTenantIdentifier.ShouldBe("example.com");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithLocalhostDomain_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext("localhost");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Could not extract tenant from request");
		exception.AttemptedTenantIdentifier.ShouldBe("localhost");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithEmptySubdomain_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext(".example.com");

		// Act & Assert
		await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveTenantAsync_WithNonExistentTenant_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext("nonexistent.example.com");

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("nonexistent", It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No active tenant found for nonexistent");
		exception.AttemptedTenantIdentifier.ShouldBe("nonexistent.example.com");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithInactiveTenant_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext("inactive.example.com");
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = false };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("inactive", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No active tenant found for inactive");
		exception.AttemptedTenantIdentifier.ShouldBe("inactive.example.com");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithMultipleLevelSubdomain_ExtractsFirstPart()
	{
		// Arrange
		var context = CreateHttpContext("test.acme.example.com");
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("test", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Subdomain:test");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithCustomExcludedSubdomains_RespectsConfiguration()
	{
		// Arrange
		var customOptions = new SubdomainTenantResolverOptions
		{
			ExcludedSubdomains = ["custom", "special"]
		};
		_mockOptions.Setup(x => x.Value).Returns(customOptions);

		var resolver = new SubdomainTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
		var context = CreateHttpContext("custom.tenant.example.com");
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("tenant", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Subdomain:tenant");
	}

	[Fact]
	public async Task ResolveTenantAsync_PassesCancellationTokenToTenantLookupService()
	{
		// Arrange
		var context = CreateHttpContext("test.example.com");
		var cancellationToken = new CancellationTokenSource().Token;
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("test", cancellationToken))
			.ReturnsAsync(tenantInfo);

		// Act
		await _resolver.GetTenantContextAsync(context, cancellationToken);

		// Assert
		_mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("test", cancellationToken), Times.Once);
	}

	private static DefaultHttpContext CreateHttpContext(string host = "localhost")
	{
		var context = new DefaultHttpContext();
		context.Request.Host = new HostString(host);
		context.Request.Scheme = "https";
		context.User = new ClaimsPrincipal();
		return context;
	}
}
