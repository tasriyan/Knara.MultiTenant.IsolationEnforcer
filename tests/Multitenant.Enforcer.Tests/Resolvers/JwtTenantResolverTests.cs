using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.Resolvers;
using System.Security.Claims;

namespace MultiTenant.Enforcer.Tests.Resolvers;

public class JwtTenantResolverTests
{
    private readonly Mock<ILogger<JwtTenantResolver>> _mockLogger;
    private readonly JwtTenantResolver _resolver;

    public JwtTenantResolverTests()
    {
        _mockLogger = new Mock<ILogger<JwtTenantResolver>>();
        _resolver = new JwtTenantResolver(_mockLogger.Object);
    }

    [Theory]
    [InlineData("role", "SystemAdmin")]
    [InlineData("system_access", "true")]
    public async Task ResolveTenantAsync_WithSystemAdminClaims_ReturnsSystemContext(string claimType, string claimValue)
    {
        // Arrange
        var context = CreateHttpContext();
        var claims = new[] { new Claim(claimType, claimValue) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert
        Assert.True(result.IsSystemContext);
        Assert.Equal("JWT-System", result.ContextSource);
        Assert.Equal(Guid.Empty, result.TenantId);
    }

    [Theory]
    [InlineData("tenant_id")]
    [InlineData("tenantId")]
    [InlineData("tid")]
    public async Task ResolveTenantAsync_WithValidTenantClaim_ReturnsTenantContext(string claimType)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = CreateHttpContext();
        var claims = new[] { new Claim(claimType, tenantId.ToString()) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert
        Assert.False(result.IsSystemContext);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("JWT", result.ContextSource);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithSystemAdminAndTenantClaim_ReturnsSystemContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var context = CreateHttpContext();
        var claims = new[]
        {
            new Claim("role", "SystemAdmin"),
            new Claim("tenant_id", tenantId.ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert - System admin takes precedence
        Assert.True(result.IsSystemContext);
        Assert.Equal("JWT-System", result.ContextSource);
        Assert.Equal(Guid.Empty, result.TenantId);
    }

    [Theory]
    [InlineData("invalid-guid")]
    [InlineData("")]
    [InlineData("not-a-guid-at-all")]
    public async Task ResolveTenantAsync_WithInvalidTenantId_ThrowsTenantResolutionException(string invalidTenantId)
    {
        // Arrange
        var context = CreateHttpContext();
        var claims = new[] { new Claim("tenant_id", invalidTenantId) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => _resolver.ResolveTenantAsync(context));
        
        Assert.Equal("No tenant information found in JWT token", exception.Message);
        Assert.Equal("JWT token missing tenant_id claim", exception.AttemptedTenantIdentifier);
        Assert.Equal("JWT", exception.ResolutionMethod);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNoTenantClaims_ThrowsTenantResolutionException()
    {
        // Arrange
        var context = CreateHttpContext();
        var claims = new[] { new Claim("email", "user@example.com") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => _resolver.ResolveTenantAsync(context));
        
        Assert.Equal("No tenant information found in JWT token", exception.Message);
        Assert.Equal("JWT token missing tenant_id claim", exception.AttemptedTenantIdentifier);
        Assert.Equal("JWT", exception.ResolutionMethod);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNoClaims_ThrowsTenantResolutionException()
    {
        // Arrange
        var context = CreateHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => _resolver.ResolveTenantAsync(context));
        
        Assert.Equal("No tenant information found in JWT token", exception.Message);
        Assert.Equal("JWT token missing tenant_id claim", exception.AttemptedTenantIdentifier);
        Assert.Equal("JWT", exception.ResolutionMethod);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithMultipleTenantClaims_UsesFirstValidClaim()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var context = CreateHttpContext();
        var claims = new[]
        {
            new Claim("tenant_id", tenantId1.ToString()),
            new Claim("tenantId", tenantId2.ToString()),
            new Claim("tid", Guid.NewGuid().ToString())
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var result = await _resolver.ResolveTenantAsync(context);

        // Assert - Should use first valid claim (tenant_id)
        Assert.False(result.IsSystemContext);
        Assert.Equal(tenantId1, result.TenantId);
        Assert.Equal("JWT", result.ContextSource);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }
}