using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.Resolvers;

namespace MultiTenant.Enforcer.Tests.Resolvers;

public class CompositeTenantResolverTests
{
    private readonly Mock<ILogger<CompositeTenantResolver>> _mockLogger;
    private readonly Mock<ITenantResolver> _mockResolver1;
    private readonly Mock<ITenantResolver> _mockResolver2;
    private readonly Mock<ITenantResolver> _mockResolver3;

    public CompositeTenantResolverTests()
    {
        _mockLogger = new Mock<ILogger<CompositeTenantResolver>>();
        _mockResolver1 = new Mock<ITenantResolver>();
        _mockResolver2 = new Mock<ITenantResolver>();
        _mockResolver3 = new Mock<ITenantResolver>();
    }

    [Fact]
    public async Task ResolveTenantAsync_WithFirstResolverSuccessful_ReturnsFirstResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expectedContext = TenantContext.ForTenant(tenantId, "Resolver1");
        var context = new DefaultHttpContext();
        
        _mockResolver1.Setup(x => x.ResolveTenantAsync(context))
                     .ReturnsAsync(expectedContext);
        
        var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act
        var result = await compositeResolver.ResolveTenantAsync(context);

        // Assert
        Assert.Equal(expectedContext.TenantId, result.TenantId);
        Assert.Equal(expectedContext.ContextSource, result.ContextSource);
        
        _mockResolver1.Verify(x => x.ResolveTenantAsync(context), Times.Once);
        _mockResolver2.Verify(x => x.ResolveTenantAsync(context), Times.Never);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithFirstResolverFailing_TriesSecondResolver()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expectedContext = TenantContext.ForTenant(tenantId, "Resolver2");
        var context = new DefaultHttpContext();
        
        _mockResolver1.Setup(x => x.ResolveTenantAsync(context))
                     .ThrowsAsync(new TenantResolutionException("Resolver1 failed"));
        
        _mockResolver2.Setup(x => x.ResolveTenantAsync(context))
                     .ReturnsAsync(expectedContext);
        
        var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act
        var result = await compositeResolver.ResolveTenantAsync(context);

        // Assert
        Assert.Equal(expectedContext.TenantId, result.TenantId);
        Assert.Equal(expectedContext.ContextSource, result.ContextSource);
        
        _mockResolver1.Verify(x => x.ResolveTenantAsync(context), Times.Once);
        _mockResolver2.Verify(x => x.ResolveTenantAsync(context), Times.Once);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithAllResolversFailing_ThrowsCompositeException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        
        _mockResolver1.Setup(x => x.ResolveTenantAsync(context))
                     .ThrowsAsync(new TenantResolutionException("Resolver1 failed"));
        
        _mockResolver2.Setup(x => x.ResolveTenantAsync(context))
                     .ThrowsAsync(new TenantResolutionException("Resolver2 failed"));
        
        _mockResolver3.Setup(x => x.ResolveTenantAsync(context))
                     .ThrowsAsync(new TenantResolutionException("Resolver3 failed"));
        
        var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object, _mockResolver3.Object };
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => compositeResolver.ResolveTenantAsync(context));
        
        Assert.Contains("All tenant resolution strategies failed", exception.Message);
        Assert.Equal("Composite", exception.ResolutionMethod);
        
        _mockResolver1.Verify(x => x.ResolveTenantAsync(context), Times.Once);
        _mockResolver2.Verify(x => x.ResolveTenantAsync(context), Times.Once);
        _mockResolver3.Verify(x => x.ResolveTenantAsync(context), Times.Once);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNonTenantResolutionException_StillTriesNextResolver()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expectedContext = TenantContext.ForTenant(tenantId, "Resolver2");
        var context = new DefaultHttpContext();
        
        _mockResolver1.Setup(x => x.ResolveTenantAsync(context))
                     .ThrowsAsync(new InvalidOperationException("Unexpected error"));
        
        _mockResolver2.Setup(x => x.ResolveTenantAsync(context))
                     .ReturnsAsync(expectedContext);
        
        var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => compositeResolver.ResolveTenantAsync(context));
        
        // Non-TenantResolutionExceptions should bubble up immediately
        Assert.Equal("Unexpected error", exception.Message);
        
        _mockResolver1.Verify(x => x.ResolveTenantAsync(context), Times.Once);
        _mockResolver2.Verify(x => x.ResolveTenantAsync(context), Times.Never);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithEmptyResolvers_ThrowsCompositeException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var resolvers = Array.Empty<ITenantResolver>();
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(
            () => compositeResolver.ResolveTenantAsync(context));
        
        Assert.Contains("All tenant resolution strategies failed", exception.Message);
        Assert.Equal("Composite", exception.ResolutionMethod);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithSystemContext_ReturnsSystemContext()
    {
        // Arrange
        var systemContext = TenantContext.SystemContext("System");
        var context = new DefaultHttpContext();
        
        _mockResolver1.Setup(x => x.ResolveTenantAsync(context))
                     .ReturnsAsync(systemContext);
        
        var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
        var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

        // Act
        var result = await compositeResolver.ResolveTenantAsync(context);

        // Assert
        Assert.True(result.IsSystemContext);
        Assert.Equal("System", result.ContextSource);
        Assert.Equal(Guid.Empty, result.TenantId);
    }
}