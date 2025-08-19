using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;
using System.Security.Claims;

namespace Multitenant.Enforcer.Tests.Resolvers;

public class PathTenantResolverTests
{
	private readonly Mock<ILogger<PathTenantResolver>> _mockLogger;
	private readonly Mock<ITenantLookupService> _mockTenantLookupService;
	private readonly Mock<IOptions<PathTenantResolverOptions>> _mockOptions;
	private readonly PathTenantResolverOptions _defaultOptions;
	private readonly PathTenantResolver _resolver;

	public PathTenantResolverTests()
	{
		_mockLogger = new Mock<ILogger<PathTenantResolver>>();
		_mockTenantLookupService = new Mock<ITenantLookupService>();
		_mockOptions = new Mock<IOptions<PathTenantResolverOptions>>();
		_defaultOptions = new PathTenantResolverOptions
		{
			ExcludedPaths = ["api", "admin", "health"],
			SystemAdminClaimTypes = ["role"],
			SystemAdminClaimValue = "SystemAdmin"
		};

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_resolver = new PathTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithValidPathSegment_ReturnsCorrectTenant()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/tenant1/dashboard";
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("tenant1", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.IsSystemContext.ShouldBeFalse();
		result.ContextSource.ShouldBe("Header:tenant1");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithExcludedPathSegment_UsesNextSegment()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/tenant2/users";
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("tenant2", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Header:tenant2");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithMultipleExcludedSegments_UsesFirstNonExcluded()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/admin/tenant3/reports";
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("tenant3", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Header:tenant3");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithEmptyPath_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "";

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No subdomain found in request");
		exception.AttemptedTenantIdentifier.ShouldBe("localhost");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithRootPath_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/";

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No subdomain found in request");
		exception.AttemptedTenantIdentifier.ShouldBe("localhost");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithOnlyExcludedPaths_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/admin";

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No subdomain found in request");
		exception.AttemptedTenantIdentifier.ShouldBe("localhost");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithNonExistentTenant_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/nonexistent/dashboard";

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("nonexistent", It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo?)null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No active tenant found for domain: nonexistent");
		exception.AttemptedTenantIdentifier.ShouldBe("nonexistent");
		exception.ResolutionMethod.ShouldBe("Header");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithInactiveTenant_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/inactive/dashboard";
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = false };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("inactive", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No active tenant found for domain: inactive");
		exception.AttemptedTenantIdentifier.ShouldBe("inactive");
		exception.ResolutionMethod.ShouldBe("Header");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithCustomExcludedPaths_RespectsConfiguration()
	{
		// Arrange
		var customOptions = new PathTenantResolverOptions
		{
			ExcludedPaths = ["custom", "special"]
		};
		_mockOptions.Setup(x => x.Value).Returns(customOptions);

		var resolver = new PathTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
		var context = CreateHttpContext();
		context.Request.Path = "/custom/tenant4/settings";
		var tenantId = Guid.NewGuid();
		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("tenant4", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act
		var result = await resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("Header:tenant4");
	}

	[Fact]
	public async Task ResolveTenantAsync_PassesCancellationTokenToTenantLookupService()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/test/dashboard";
		var cancellationToken = new CancellationTokenSource().Token;
		var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("test", cancellationToken))
			.ReturnsAsync(tenantInfo);

		// Act
		await _resolver.ResolveTenantAsync(context, cancellationToken);

		// Assert
		_mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("test", cancellationToken), Times.Once);
	}

	private static DefaultHttpContext CreateHttpContext()
	{
		var context = new DefaultHttpContext();
		context.Request.Host = new HostString("localhost");
		context.Request.Scheme = "https";
		context.User = new ClaimsPrincipal();
		return context;
	}
}
