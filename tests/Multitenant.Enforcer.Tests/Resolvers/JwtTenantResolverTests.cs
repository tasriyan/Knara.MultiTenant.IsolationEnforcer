using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;
using System.Security.Claims;

namespace Multitenant.Enforcer.Tests.Resolvers;

public class JwtTenantResolverTests
{
	private readonly Mock<ILogger<JwtTenantResolver>> _mockLogger;
	private readonly Mock<ITenantResolver> _mockSubdomainResolver;
	private readonly Mock<IOptions<JwtTenantResolverOptions>> _mockOptions;
	private readonly JwtTenantResolverOptions _defaultOptions;
	private readonly JwtTenantResolver _resolver;

	public JwtTenantResolverTests()
	{
		_mockLogger = new Mock<ILogger<JwtTenantResolver>>();
		_mockSubdomainResolver = new Mock<ITenantResolver>();
		_mockOptions = new Mock<IOptions<JwtTenantResolverOptions>>();
		_defaultOptions = new JwtTenantResolverOptions
		{
			TenantIdClaimTypes = ["tenant_id", "tenantId", "tid"],
			SystemAdminClaimTypes = ["role"],
			SystemAdminClaimValue = "SystemAdmin"
		};

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_resolver = new JwtTenantResolver(_mockLogger.Object, _mockSubdomainResolver.Object, _mockOptions.Object);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithValidTenantIdClaim_ReturnsCorrectTenant()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var subdomainTenantContext = TenantContext.ForTenant(tenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(subdomainTenantContext);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.IsSystemContext.ShouldBeFalse();
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithDifferentClaimType_ReturnsCorrectTenant()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenantId", tenantId.ToString()));

		var subdomainTenantContext = TenantContext.ForTenant(tenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(subdomainTenantContext);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithNoTenantClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("other_claim", "value"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No tenant information found in JWT token");
		exception.AttemptedTenantIdentifier.ShouldBe("JWT token missing tenant_id claim");
		exception.ResolutionMethod.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithInvalidGuidClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", "not-a-guid"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No tenant information found in JWT token");
		exception.AttemptedTenantIdentifier.ShouldBe("JWT token missing tenant_id claim");
		exception.ResolutionMethod.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithEmptyTenantClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", ""));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No tenant information found in JWT token");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithMismatchedTenantIds_ThrowsTenantResolutionException()
	{
		// Arrange
		var jwtTenantId = Guid.NewGuid();
		var subdomainTenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", jwtTenantId.ToString()));

		var subdomainTenantContext = TenantContext.ForTenant(subdomainTenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(subdomainTenantContext);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Tenant is not authorized to access this subdomain");
		exception.AttemptedTenantIdentifier.ShouldBe("localhost");
		exception.ResolutionMethod.ShouldBe("Subdomain");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithMatchingTenantIds_ReturnsSuccessfully()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var subdomainTenantContext = TenantContext.ForTenant(tenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(subdomainTenantContext);

		// Act
		var result = await _resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_WithCustomClaimTypes_FindsCorrectClaim()
	{
		// Arrange
		var customOptions = new JwtTenantResolverOptions
		{
			TenantIdClaimTypes = ["custom_tenant", "org_id"]
		};
		_mockOptions.Setup(x => x.Value).Returns(customOptions);

		var resolver = new JwtTenantResolver(_mockLogger.Object, _mockSubdomainResolver.Object, _mockOptions.Object);
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("custom_tenant", tenantId.ToString()));

		var subdomainTenantContext = TenantContext.ForTenant(tenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(subdomainTenantContext);

		// Act
		var result = await resolver.ResolveTenantAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task ResolveTenantAsync_PassesCancellationTokenToSubdomainResolver()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));
		var cancellationToken = new CancellationTokenSource().Token;

		var subdomainTenantContext = TenantContext.ForTenant(tenantId, "Subdomain");
		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, cancellationToken))
			.ReturnsAsync(subdomainTenantContext);

		// Act
		await _resolver.ResolveTenantAsync(context, cancellationToken);

		// Assert
		_mockSubdomainResolver.Verify(x => x.ResolveTenantAsync(context, cancellationToken), Times.Once);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithSubdomainResolverException_PropagatesException()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		_mockSubdomainResolver.Setup(x => x.ResolveTenantAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("Subdomain error", "test", "Subdomain"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.ResolveTenantAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Subdomain error");
	}

	private static DefaultHttpContext CreateHttpContext()
	{
		var context = new DefaultHttpContext();
		context.Request.Host = new HostString("localhost");
		context.Request.Scheme = "https";
		context.User = new ClaimsPrincipal();
		return context;
	}

	private static ClaimsPrincipal CreateClaimsPrincipal(params Claim[] claims)
	{
		var identity = new ClaimsIdentity(claims, "test");
		return new ClaimsPrincipal(identity);
	}
}
