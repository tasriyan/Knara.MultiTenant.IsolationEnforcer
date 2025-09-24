using Knara.MultiTenant.IsolationEnforcer.AspNetCore.Middleware;
using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace UnitTests.AspnetCore;

public class TenantContextMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<TenantContextMiddleware>> _mockLogger;
    private readonly Mock<ITenantContextAccessor> _mockTenantAccessor;
    private readonly Mock<ITenantResolver> _mockTenantResolver;
    private readonly TenantContextMiddleware _middleware;

    public TenantContextMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<TenantContextMiddleware>>();
        _mockTenantAccessor = new Mock<ITenantContextAccessor>();
        _mockTenantResolver = new Mock<ITenantResolver>();
        _middleware = new TenantContextMiddleware(_mockNext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithSuccessfulTenantResolution_SetsTenantContextAndCallsNext()
    {
        // Arrange
        var context = CreateHttpContext();
        var tenantId = Guid.NewGuid();
        var tenantContext = TenantContext.ForTenant(tenantId, "JWT");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(tenantContext);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        _mockTenantAccessor.Verify(x => x.SetContext(tenantContext), Times.Once);
        _mockNext.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithSystemContext_SetsTenantContextAndCallsNext()
    {
        // Arrange
        var context = CreateHttpContext();
        var tenantContext = TenantContext.SystemContext("SystemAdmin");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(tenantContext);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        _mockTenantAccessor.Verify(x => x.SetContext(tenantContext), Times.Once);
        _mockNext.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantResolutionException_HandlesErrorAndDoesNotCallNext()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new TenantResolutionException("Failed to resolve tenant", "invalid-domain", "Subdomain");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        context.Response.StatusCode.ShouldBe(400);
        context.Response.ContentType.ShouldBe("application/json");
        
        _mockTenantAccessor.Verify(x => x.SetContext(It.IsAny<TenantContext>()), Times.Never);
        _mockNext.Verify(x => x(context), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantResolutionException_WritesCorrectErrorResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new TenantResolutionException("Failed to resolve tenant", "tenant1.example.com", "Subdomain");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
        errorResponse.GetProperty("Error").GetString().ShouldBe("Invalid tenant context");
        errorResponse.GetProperty("Message").GetString().ShouldBe("Failed to resolve tenant");
        errorResponse.GetProperty("Details").GetProperty("AttemptedIdentifier").GetString().ShouldBe("tenant1.example.com");
        errorResponse.GetProperty("Details").GetProperty("ResolutionMethod").GetString().ShouldBe("Subdomain");
    }

    [Fact]
    public async Task InvokeAsync_WithUnexpectedException_HandlesErrorAndDoesNotCallNext()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Unexpected error occurred");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        context.Response.StatusCode.ShouldBe(500);
        context.Response.ContentType.ShouldBe("application/json");
        
        _mockTenantAccessor.Verify(x => x.SetContext(It.IsAny<TenantContext>()), Times.Never);
        _mockNext.Verify(x => x(context), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithUnexpectedException_WritesCorrectErrorResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Unexpected error occurred");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
        errorResponse.GetProperty("Error").GetString().ShouldBe("Internal server error");
        errorResponse.GetProperty("Message").GetString().ShouldBe("An unexpected error occurred while processing the tenant context");
    }

    [Fact]
    public async Task InvokeAsync_CreatesLoggingScopeWithTenantInformation()
    {
        // Arrange
        var context = CreateHttpContext("/api/users", "GET");
        var tenantId = Guid.NewGuid();
        var tenantContext = TenantContext.ForTenant(tenantId, "JWT");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(tenantContext);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert - Verify that BeginScope was called with correct parameters
        _mockLogger.Verify(
            x => x.BeginScope(It.Is<Dictionary<string, object>>(d => 
                d["TenantId"].Equals(tenantId) &&
                d["IsSystemContext"].Equals(false) &&
                d["TenantSource"].Equals("JWT") &&
                d["RequestPath"].Equals("/api/users") &&
                d["RequestMethod"].Equals("GET"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithSystemContext_CreatesCorrectLoggingScope()
    {
        // Arrange
        var context = CreateHttpContext("/admin/reports", "POST");
        var tenantContext = TenantContext.SystemContext("SystemAdmin");

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(tenantContext);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        _mockLogger.Verify(
            x => x.BeginScope(It.Is<Dictionary<string, object>>(d => 
                d["TenantId"].Equals(Guid.Empty) &&
                d["IsSystemContext"].Equals(true) &&
                d["TenantSource"].Equals("SystemAdmin") &&
                d["RequestPath"].Equals("/admin/reports") &&
                d["RequestMethod"].Equals("POST"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_PassesToResolver()
    {
        // Arrange
        var context = CreateHttpContext();
        var tenantContext = TenantContext.ForTenant(Guid.NewGuid(), "JWT");
        var cancellationToken = new CancellationToken();

        _mockTenantResolver.Setup(x => x.GetTenantContextAsync(context, cancellationToken))
                         .ReturnsAsync(tenantContext);

        // Act
        await _middleware.InvokeAsync(context, _mockTenantAccessor.Object, _mockTenantResolver.Object);

        // Assert
        _mockTenantResolver.Verify(x => x.GetTenantContextAsync(context, cancellationToken), Times.Once);
    }

    private static DefaultHttpContext CreateHttpContext(string path = "/", string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }
}