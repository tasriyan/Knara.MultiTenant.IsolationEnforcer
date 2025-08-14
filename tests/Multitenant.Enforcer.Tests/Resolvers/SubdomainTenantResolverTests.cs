using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.Resolvers;
using System.Security.Claims;

namespace MultiTenant.Enforcer.Tests.Resolvers;

public class SubdomainTenantResolverTests
{
    private readonly Mock<ILogger<SubdomainTenantResolver>> _mockLogger;
    private readonly Mock<ITenantLookupService> _mockTenantLookupService;
    private readonly SubdomainTenantResolver _resolver;

    public SubdomainTenantResolverTests()
    {
        _mockLogger = new Mock<ILogger<SubdomainTenantResolver>>();
        _mockTenantLookupService = new Mock<ITenantLookupService>();
        _resolver = new SubdomainTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithSystemAdminClaim_ReturnsSystemContext()
    {
        // Arrange
        var context = CreateHttpContext("tenant1.example.com");
        var claims = new[] { new Claim("role", "SystemAdmin") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert
        Assert.True(result.IsSystemContext);
        Assert.Equal("SystemAdmin-JWT", result.ContextSource);
        Assert.Equal(Guid.Empty, result.TenantId);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithValidSubdomain_ReturnsTenantContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var subdomain = "tenant1";
        var context = CreateHttpContext($"{subdomain}.example.com");
        
        _mockTenantLookupService
            .Setup(x => x.GetTenantIdByDomainAsync(subdomain))
            .ReturnsAsync(tenantId);

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert
        Assert.False(result.IsSystemContext);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal($"Subdomain:{subdomain}", result.ContextSource);
    }

    [Theory]
    [InlineData("www.example.com")]
    [InlineData("localhost")]
    [InlineData("example.com")]
    public async Task ResolveTenantAsync_WithInvalidSubdomain_ThrowsTenantResolutionException(string host)
    {
        // Arrange
        var context = CreateHttpContext(host);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => _resolver.ResolveTenantAsync(context));
        
        Assert.Equal("No subdomain found in request", exception.Message);
        Assert.Equal(host, exception.AttemptedTenantIdentifier);
        Assert.Equal("Subdomain", exception.ResolutionMethod);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNonExistentTenant_ThrowsTenantResolutionException()
    {
        // Arrange
        var subdomain = "nonexistent";
        var context = CreateHttpContext($"{subdomain}.example.com");
        
        _mockTenantLookupService
            .Setup(x => x.GetTenantIdByDomainAsync(subdomain))
            .ReturnsAsync((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => _resolver.ResolveTenantAsync(context));
        
        Assert.Equal($"No active tenant found for domain: {subdomain}", exception.Message);
        Assert.Equal(subdomain, exception.AttemptedTenantIdentifier);
        Assert.Equal("Subdomain", exception.ResolutionMethod);
    }

    [Theory]
    [InlineData("tenant1.example.com", "tenant1")]
    [InlineData("api.tenant2.example.com", "api")]
    [InlineData("sub.domain.example.com", "sub")]
    public async Task ExtractSubdomain_WithValidHosts_ExtractsCorrectSubdomain(string host, string expectedSubdomain)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = CreateHttpContext(host);
        
        _mockTenantLookupService
            .Setup(x => x.GetTenantIdByDomainAsync(expectedSubdomain))
            .ReturnsAsync(tenantId);

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert
        Assert.Equal($"Subdomain:{expectedSubdomain}", result.ContextSource);
        _mockTenantLookupService.Verify(x => x.GetTenantIdByDomainAsync(expectedSubdomain), Times.Once);
    }

    private static HttpContext CreateHttpContext(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }
}