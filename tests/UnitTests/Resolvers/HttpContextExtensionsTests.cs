using Knara.MultiTenant.IsolationEnforcer.TenantResolvers.Strategies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace UnitTests.DomainResolvers;

public class HttpContextExtensionsTests
{
	#region IsUserASystemAdmin Tests

	[Fact]
	public void IsUserASystemAdmin_WithNullUser_ReturnsFalse()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = null;

		// Act
		var result = context.IsUserASystemAdmin(["role"], "SystemAdmin");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsUserASystemAdmin_WithUnauthenticatedUser_ReturnsFalse()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = new ClaimsPrincipal();

		// Act
		var result = context.IsUserASystemAdmin(["role"], "SystemAdmin");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsUserASystemAdmin_WithCorrectClaim_ReturnsTrue()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("role", "SystemAdmin"));

		// Act
		var result = context.IsUserASystemAdmin(["role"], "SystemAdmin");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsUserASystemAdmin_WithMultipleClaimTypes_FindsCorrectOne()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("admin_role", "SystemAdmin"));

		// Act
		var result = context.IsUserASystemAdmin(["role", "admin_role"], "SystemAdmin");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsUserASystemAdmin_WithWrongClaimValue_ReturnsFalse()
	{
		// Arrange
		var context = CreateHttpContext();
		context.User = CreateClaimsPrincipal(new Claim("role", "User"));

		// Act
		var result = context.IsUserASystemAdmin(["role"], "SystemAdmin");

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region ExtractSubdomainFromDomain Tests

	[Fact]
	public void ExtractSubdomainFromDomain_WithValidSubdomain_ReturnsSubdomain()
	{
		// Arrange
		var context = CreateHttpContext("acme.example.com");

		// Act
		var result = context.TenantFromSubdomain([]);

		// Assert
		result.ShouldBe("acme");
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithExcludedSubdomain_ReturnsSecondPart()
	{
		// Arrange
		var context = CreateHttpContext("www.globex.example.com");

		// Act
		var result = context.TenantFromSubdomain(["www"]);

		// Assert
		result.ShouldBe("globex");
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithTwoPartsOnly_ReturnsEmpty()
	{
		// Arrange
		var context = CreateHttpContext("example.com");

		// Act
		var result = context.TenantFromSubdomain([]);

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithOnePartOnly_ReturnsEmpty()
	{
		// Arrange
		var context = CreateHttpContext("localhost");

		// Act
		var result = context.TenantFromSubdomain([]);

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithExcludedFirstPartButOnlyThreeParts_ReturnsSecondPart()
	{
		// Arrange
		var context = CreateHttpContext("www.example.com");

		// Act
		var result = context.TenantFromSubdomain(["www"]);

		// Assert
		result.ShouldBe("example");
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithCaseInsensitiveExclusion_ReturnsSecondPart()
	{
		// Arrange
		var context = CreateHttpContext("WWW.tenant.example.com");

		// Act
		var result = context.TenantFromSubdomain(["www"]);

		// Assert
		result.ShouldBe("tenant");
	}

	[Fact]
	public void ExtractSubdomainFromDomain_WithNullExcludedSubdomains_ReturnsFirstPart()
	{
		// Arrange
		var context = CreateHttpContext("test.example.com");

		// Act
		var result = context.TenantFromSubdomain(null);

		// Assert
		result.ShouldBe("test");
	}

	#endregion

	#region ExtractSubdomaintFromQuery Tests

	[Fact]
	public void ExtractSubdomaintFromQuery_WithValidQueryParameter_ReturnsValue()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.QueryString = new QueryString("?tenant=acme");

		// Act
		var result = context.TenantFromQuery(["tenant"]);

		// Assert
		result.ShouldBe("acme");
	}

	[Fact]
	public void ExtractSubdomaintFromQuery_WithMultipleParameters_ReturnsFirstMatch()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.QueryString = new QueryString("?tenant=first&tenantId=second");

		// Act
		var result = context.TenantFromQuery(["tenant", "tenantId"]);

		// Assert
		result.ShouldBe("first");
	}

	[Fact]
	public void ExtractSubdomaintFromQuery_WithNoMatchingParameter_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.QueryString = new QueryString("?other=value");

		// Act
		var result = context.TenantFromQuery(["tenant"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExtractSubdomaintFromQuery_WithEmptyQuery_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();

		// Act
		var result = context.TenantFromQuery(["tenant"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExtractSubdomaintFromQuery_WithEmptyParameterValue_ReturnsEmpty()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.QueryString = new QueryString("?tenant=");

		// Act
		var result = context.TenantFromQuery(["tenant"]);

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion

	#region ExtractSubdomainFromHeader Tests

	[Fact]
	public void ExtractSubdomainFromHeader_WithValidHeader_ReturnsValue()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Headers["X-Tenant"] = "acme";

		// Act
		var result = context.TenantFromHeader(["X-Tenant"]);

		// Assert
		result.ShouldBe("acme");
	}

	[Fact]
	public void ExtractSubdomainFromHeader_WithMultipleHeaders_ReturnsFirstMatch()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Headers["X-Tenant-ID"] = "first";
		context.Request.Headers["X-Tenant"] = "second";

		// Act
		var result = context.TenantFromHeader(["X-Tenant-ID", "X-Tenant"]);

		// Assert
		result.ShouldBe("first");
	}

	[Fact]
	public void ExtractSubdomainFromHeader_WithNoMatchingHeader_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Headers["Other-Header"] = "value";

		// Act
		var result = context.TenantFromHeader(["X-Tenant"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExtractSubdomainFromHeader_WithEmptyHeaders_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();

		// Act
		var result = context.TenantFromHeader(["X-Tenant"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExtractSubdomainFromHeader_WithEmptyHeaderValue_ReturnsEmpty()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Headers["X-Tenant"] = "";

		// Act
		var result = context.TenantFromHeader(["X-Tenant"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	#endregion

	#region ExtractSubdomainFromPath Tests

	[Fact]
	public void ExtractSubdomainFromPath_WithValidPath_ReturnsFirstSegment()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/tenant1/dashboard";

		// Act
		var result = context.TenantFromPath([]);

		// Assert
		result.ShouldBe("tenant1");
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithExcludedFirstSegment_ReturnsSecondSegment()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/tenant1/users";

		// Act
		var result = context.TenantFromPath(["api"]);

		// Assert
		result.ShouldBe("tenant1");
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithMultipleExcludedSegments_ReturnsFirstNonExcluded()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/v1/tenant1/dashboard";

		// Act
		var result = context.TenantFromPath(["api", "v1"]);

		// Assert
		result.ShouldBe("tenant1");
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithEmptyPath_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "";

		// Act
		var result = context.TenantFromPath([]);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithRootPath_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/";

		// Act
		var result = context.TenantFromPath([]);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithAllSegmentsExcluded_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/api/v1";

		// Act
		var result = context.TenantFromPath(["api", "v1"]);

		// Assert
		result.ShouldBeNullOrWhiteSpace();
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithCaseInsensitiveExclusion_ReturnsCorrectSegment()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = "/API/tenant1/dashboard";

		// Act
		var result = context.TenantFromPath(["api"]);

		// Assert
		result.ShouldBe("tenant1");
	}

	[Fact]
	public void ExtractSubdomainFromPath_WithNullPath_ReturnsNull()
	{
		// Arrange
		var context = CreateHttpContext();
		context.Request.Path = PathString.Empty;

		// Act
		var result = context.TenantFromPath([]);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region Helper Methods

	private static DefaultHttpContext CreateHttpContext(string host = "localhost")
	{
		var context = new DefaultHttpContext();
		context.Request.Host = new HostString(host);
		context.Request.Scheme = "https";
		context.User = new ClaimsPrincipal();
		return context;
	}

	private static ClaimsPrincipal CreateClaimsPrincipal(params Claim[] claims)
	{
		var identity = new ClaimsIdentity(claims, "test");
		return new ClaimsPrincipal(identity);
	}

	#endregion
}
