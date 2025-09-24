using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace UnitTests.Resolvers;

public class HeaderTenantResolverTests
{
    private readonly Mock<ILogger<HeaderQueryTenantResolver>> _mockLogger;
    private readonly Mock<ITenantLookupService> _mockTenantLookupService;
    private readonly Mock<IOptions<HeaderQueryTenantResolverOptions>> _mockOptions;
    private readonly HeaderQueryTenantResolverOptions _defaultOptions;
    private readonly HeaderQueryTenantResolver _resolver;

    public HeaderTenantResolverTests()
    {
        _mockLogger = new Mock<ILogger<HeaderQueryTenantResolver>>();
        _mockTenantLookupService = new Mock<ITenantLookupService>();
        _mockOptions = new Mock<IOptions<HeaderQueryTenantResolverOptions>>();
        _defaultOptions = new HeaderQueryTenantResolverOptions
        {
            IncludedHeaders = ["X-Tenant-ID", "X-Tenant"],
            IncludedQueryParameters = ["tenant", "tenant_id", "tenantId", "tid"],
            SystemAdminClaimTypes = ["role"],
            SystemAdminClaimValue = "SystemAdmin"
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
        _resolver = new HeaderQueryTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
    }

    #region System Admin Tests

    [Fact]
    public async Task ResolveTenantAsync_WithSystemAdminRole_ReturnsSystemContext()
    {
        // Arrange
        var context = CreateHttpContext();
        context.User = CreateClaimsPrincipal(new Claim("role", "SystemAdmin"));
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _resolver.GetTenantContextAsync(context, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSystemContext.ShouldBeTrue();
        result.TenantId.ShouldBe(Guid.Empty);
        
        // Verify no tenant lookup was attempted
        _mockTenantLookupService.Verify(x => x.GetTenantInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithSystemAdminClaimInDifferentClaimType_ReturnsSystemContext()
    {
        // Arrange
        _defaultOptions.SystemAdminClaimTypes = ["admin_role", "role"];
        var context = CreateHttpContext();
        context.User = CreateClaimsPrincipal(new Claim("admin_role", "SystemAdmin"));
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await _resolver.GetTenantContextAsync(context, cancellationToken);

        // Assert
        result.IsSystemContext.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNonSystemAdminRole_DoesNotReturnSystemContext()
    {
        // Arrange
        var context = CreateHttpContext();
        context.User = CreateClaimsPrincipal(new Claim("role", "User"));
        context.Request.Headers["X-Tenant-ID"] = "test-tenant";
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("test-tenant", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(tenantInfo);


        // Act
        var result = await _resolver.GetTenantContextAsync(context, cancellationToken: CancellationToken.None);

        // Assert
        result.IsSystemContext.ShouldBeFalse();
        result.TenantId.ShouldBe(tenantInfo.Id);
    }

    #endregion

    #region Header Resolution Tests

    [Fact]
    public async Task ResolveTenantAsync_WithTenantIdInXTenantIdHeader_ResolvesFromHeader()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant-ID"] = tenantGuid.ToString();
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantGuid);
        result.IsSystemContext.ShouldBeFalse();
        result.ContextSource.ShouldBe($"HeaderQuery:{tenantGuid}");
        
        _mockTenantLookupService.Verify(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithTenantDomainInXTenantHeader_ResolvesFromHeader()
    {
        // Arrange
        var tenantDomain = "acme-corp";
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = tenantDomain;
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantGuid);
        result.ContextSource.ShouldBe($"HeaderQuery:{tenantDomain}");
        
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithMultipleHeaders_UsesFirstMatchingHeader()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant-ID"] = "first-tenant";
        context.Request.Headers["X-Tenant"] = "second-tenant";
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("first-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.ContextSource.ShouldBe("HeaderQuery:first-tenant");
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("first-tenant", It.IsAny<CancellationToken>()), Times.Once);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("second-tenant", It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Query Parameter Resolution Tests

    [Fact]
    public async Task ResolveTenantAsync_WithTenantInQueryParameter_ResolvesFromQuery()
    {
        // Arrange
        var tenantDomain = "globex";
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString("?tenant=globex");
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.TenantId.ShouldBe(tenantGuid);
        result.ContextSource.ShouldBe($"HeaderQuery:{tenantDomain}");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithTenantIdInQueryParameter_ResolvesAsGuid()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?tenantId={tenantGuid}");
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.TenantId.ShouldBe(tenantGuid);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveTenantAsync_HeaderTakesPrecedenceOverQueryParameter()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = "header-tenant";
        context.Request.QueryString = new QueryString("?tenant=query-tenant");
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("header-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.ContextSource.ShouldBe("HeaderQuery:header-tenant");
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("header-tenant", It.IsAny<CancellationToken>()), Times.Once);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync("query-tenant", It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ResolveTenantAsync_WithNoTenantIdentifier_ThrowsTenantResolutionException()
    {
        // Arrange
        var context = CreateHttpContext();
        // No headers or query parameters set

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
        
        exception.Message.ShouldBe("Could not extract tenant from request");
        exception.AttemptedTenantIdentifier.ShouldBe("localhost");
        exception.ResolutionMethod.ShouldBe("HeaderQuery");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithEmptyHeaderValue_ThrowsTenantResolutionException()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
        
        exception.Message.ShouldBe("Could not extract tenant from request");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithWhitespaceHeaderValue_ThrowsTenantResolutionException()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = "   ";

        // Act & Assert
        await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveTenantAsync_WithInactiveTenant_ThrowsTenantResolutionException()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant-ID"] = tenantGuid.ToString();
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = false };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
        
        exception.Message.ShouldBe($"No active tenant found for {tenantGuid}");
        exception.AttemptedTenantIdentifier.ShouldBe(tenantGuid.ToString());
        exception.ResolutionMethod.ShouldBe("HeaderQuery");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNonExistentTenant_ThrowsTenantResolutionException()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant-ID"] = tenantGuid.ToString();
        
        _mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantInfo?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
        
        exception.Message.ShouldBe($"No active tenant found for {tenantGuid}");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithInactiveTenantByDomain_ThrowsTenantResolutionException()
    {
        // Arrange
        var tenantDomain = "inactive-tenant";
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = tenantDomain;
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = false };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
            _resolver.GetTenantContextAsync(context, CancellationToken.None));
        
        exception.Message.ShouldBe($"No active tenant found for {tenantDomain}");
        exception.AttemptedTenantIdentifier.ShouldBe(tenantDomain);
        exception.ResolutionMethod.ShouldBe("HeaderQuery");
    }

    #endregion

    #region GUID vs Domain Resolution Tests

    [Fact]
    public async Task ResolveTenantAsync_WithValidGuid_CallsGetTenantInfoAsync()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = tenantGuid.ToString();
        
        var tenantInfo = new TenantInfo { Id = tenantGuid, IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        _mockTenantLookupService.Verify(x => x.GetTenantInfoAsync(tenantGuid, It.IsAny<CancellationToken>()), Times.Once);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveTenantAsync_WithNonGuidString_CallsGetTenantInfoByDomainAsync()
    {
        // Arrange
        var tenantDomain = "not-a-guid";
        var context = CreateHttpContext();
        context.Request.Headers["X-Tenant"] = tenantDomain;
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        await _resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        _mockTenantLookupService.Verify(x => x.GetTenantInfoByDomainAsync(tenantDomain, It.IsAny<CancellationToken>()), Times.Once);
        _mockTenantLookupService.Verify(x => x.GetTenantInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Custom Options Tests

    [Fact]
    public async Task ResolveTenantAsync_WithCustomHeaders_UsesCustomConfiguration()
    {
        // Arrange
        var customOptions = new HeaderQueryTenantResolverOptions
        {
            IncludedHeaders = ["Custom-Tenant", "App-Tenant"],
            IncludedQueryParameters = ["custom_tenant"],
            SystemAdminClaimTypes = ["custom_role"],
            SystemAdminClaimValue = "SuperAdmin"
        };
        _mockOptions.Setup(x => x.Value).Returns(customOptions);
        
        var resolver = new HeaderQueryTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
        var context = CreateHttpContext();
        context.Request.Headers["Custom-Tenant"] = "custom-value";
        
        var tenantInfo = new TenantInfo { Id = Guid.NewGuid(), IsActive = true };
        _mockTenantLookupService.Setup(x => x.GetTenantInfoByDomainAsync("custom-value", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantInfo);

        // Act
        var result = await resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.ContextSource.ShouldBe("HeaderQuery:custom-value");
    }

    [Fact]
    public async Task ResolveTenantAsync_WithCustomSystemAdminConfig_RecognizesCustomAdmin()
    {
        // Arrange
        var customOptions = new HeaderQueryTenantResolverOptions
        {
            SystemAdminClaimTypes = ["custom_role"],
            SystemAdminClaimValue = "SuperAdmin"
        };
        _mockOptions.Setup(x => x.Value).Returns(customOptions);
        
        var resolver = new HeaderQueryTenantResolver(_mockLogger.Object, _mockTenantLookupService.Object, _mockOptions.Object);
        var context = CreateHttpContext();
        context.User = CreateClaimsPrincipal(new Claim("custom_role", "SuperAdmin"));

        // Act
        var result = await resolver.GetTenantContextAsync(context, CancellationToken.None);

        // Assert
        result.IsSystemContext.ShouldBeTrue();
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
