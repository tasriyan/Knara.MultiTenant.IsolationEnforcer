using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace UnitTests.Resolvers;

public class JwtTenantResolverTests
{
	private readonly Mock<ILogger<JwtTenantResolver>> _mockLogger;
	private readonly Mock<ITenantDomainValidator> _mockDomainValidator;
	private readonly Mock<ITenantLookupService> _mockTenantLookupService;
	private readonly Mock<IOptions<JwtTenantResolverOptions>> _mockOptions;
	private readonly JwtTenantResolverOptions _defaultOptions;
	private readonly JwtTenantResolver _resolver;

	public JwtTenantResolverTests()
	{
		_mockLogger = new Mock<ILogger<JwtTenantResolver>>();
		_mockDomainValidator = new Mock<ITenantDomainValidator>();
		_mockTenantLookupService = new Mock<ITenantLookupService>();
		_mockOptions = new Mock<IOptions<JwtTenantResolverOptions>>();
		_defaultOptions = new JwtTenantResolverOptions
		{
			TenantIdClaimTypes = ["tenant_id", "tenantId", "tid"],
			SystemAdminClaimTypes = ["role"],
			SystemAdminClaimValue = "SystemAdmin",
			DomainValidationMode = TenantDomainValidationMode.ValidateAgainstSubdomain
		};

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_resolver = new JwtTenantResolver(_mockLogger.Object, _mockDomainValidator.Object, _mockTenantLookupService.Object, _mockOptions.Object);
	}

	[Fact]
	public async Task GetTenantContextAsync_WithSystemAdminClaim_ReturnsSystemContext()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("role", "SystemAdmin"));

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.IsSystemContext.ShouldBeTrue();
		result.ContextSource.ShouldBe("SystemAdmin-JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithValidGuidTenantIdClaim_ReturnsCorrectTenant()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.IsSystemContext.ShouldBeFalse();
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithValidGuidButInactiveTenant_ThrowsException()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = false };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Invalid tenant id provided in claim or claim tenant is not active or not authorized to access this subdomain.");
		exception.ResolutionMethod.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithValidGuidButDomainValidationFails_ThrowsException()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Invalid tenant id provided in claim or claim tenant is not active or not authorized to access this subdomain.");
		exception.ResolutionMethod.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithNonGuidTenantClaim_LooksUpByDomain()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", "acme"));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("acme", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithMultipleTenantClaims_ReturnsFirstValidTenant()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", "invalid,acme,contoso"));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("invalid", It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo)null);
		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("acme", It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
		_mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("invalid", It.IsAny<CancellationToken>()), Times.Once);
		_mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("acme", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetTenantContextAsync_WithMultipleTenantClaimsButDomainValidationFails_TriesNextTenant()
	{
		// Arrange
		var firstTenantId = Guid.NewGuid();
		var secondTenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", "acme,contoso"));

		var firstTenantInfo = new TenantInfo { Id = firstTenantId, IsActive = true };
		var secondTenantInfo = new TenantInfo { Id = secondTenantId, IsActive = true };
		
		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("acme", It.IsAny<CancellationToken>()))
			.ReturnsAsync(firstTenantInfo);
		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("contoso", It.IsAny<CancellationToken>()))
			.ReturnsAsync(secondTenantInfo);
		
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(firstTenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(secondTenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(secondTenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithNoTenantClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("other_claim", "value"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("No tenant information found in JWT token");
		exception.AttemptedTenantIdentifier.ShouldBe("JWT token missing tenant id claim");
		exception.ResolutionMethod.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithInvalidGuidClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", "not-a-guid"));

		_mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("not-a-guid", It.IsAny<CancellationToken>()))
			.ReturnsAsync((TenantInfo)null);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Invalid tenant id provided in claim or claim tenant is not active or not authorized to access this subdomain.");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithEmptyTenantClaim_ThrowsTenantResolutionException()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", ""));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Invalid tenant id provided in claim or claim tenant is not active or not authorized to access this subdomain.");
	}

	[Fact]
	public async Task GetTenantContextAsync_WithCustomClaimTypes_FindsCorrectClaim()
	{
		// Arrange
		var customOptions = new JwtTenantResolverOptions
		{
			TenantIdClaimTypes = ["custom_tenant", "org_id"]
		};
		_mockOptions.Setup(x => x.Value).Returns(customOptions);

		var resolver = new JwtTenantResolver(_mockLogger.Object, _mockDomainValidator.Object, _mockTenantLookupService.Object, _mockOptions.Object);
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("custom_tenant", tenantId.ToString()));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		// Act
		var result = await resolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
		result.ContextSource.ShouldBe("JWT");
	}

	[Fact]
	public async Task GetTenantContextAsync_PassesCancellationTokenToDomainValidator()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));
		var cancellationToken = new CancellationTokenSource().Token;

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, cancellationToken))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, cancellationToken))
			.ReturnsAsync(true);

		// Act
		await _resolver.GetTenantContextAsync(context, cancellationToken);

		// Assert
		_mockDomainValidator.Verify(x => x.ValidateTenantDomainAsync(tenantId, context, cancellationToken), Times.Once);
	}

	[Fact]
	public async Task GetTenantContextAsync_WithDomainValidatorException_PropagatesException()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("tenant_id", tenantId.ToString()));

		var tenantInfo = new TenantInfo { Id = tenantId, IsActive = true };
		_mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(tenantInfo);
		_mockDomainValidator.Setup(x => x.ValidateTenantDomainAsync(tenantId, context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Domain validation error"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_resolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldBe("Domain validation error");
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
