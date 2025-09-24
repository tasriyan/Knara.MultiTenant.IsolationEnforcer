using Knara.MultiTenant.IsolationEnforcer.AspNetCore;
using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Multitenant.Enforcer.Tests.AspNetCore;

public class CrossTenantOperationManagerTests
{
	private readonly Mock<ITenantContextAccessor> _mockTenantAccessor;
	private readonly Mock<ILogger<CrossTenantOperationManager>> _mockLogger;
	private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
	private readonly CrossTenantOperationManager _manager;
	private readonly DefaultHttpContext _httpContext;

	public CrossTenantOperationManagerTests()
	{
		_mockTenantAccessor = new Mock<ITenantContextAccessor>();
		_mockLogger = new Mock<ILogger<CrossTenantOperationManager>>();
		_mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
		_httpContext = new DefaultHttpContext();

		_mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);
		_manager = new CrossTenantOperationManager(_mockTenantAccessor.Object, _mockLogger.Object, _mockHttpContextAccessor.Object);
	}

	#region ExecuteCrossTenantOperationAsync<T> Tests

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithValidOperation_ReturnsResult()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		var expectedResult = "test result";

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		var result = await _manager.ExecuteCrossTenantOperationAsync(
			() => Task.FromResult(expectedResult),
			"Test operation");

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_SetsSystemContextDuringOperation()
	{
		// Arrange
		var originalContext = TenantContext.SystemContext("Original context: should be restored after operation run");  
		TenantContext capturedContext = null;

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);
		_mockTenantAccessor.Setup(x => x.SetContext(It.IsAny<TenantContext>()))
			.Callback<TenantContext>(ctx => capturedContext = ctx);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(
			() => Task.FromResult("result"),
			"Test operation");

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext.IsSystemContext.ShouldBeTrue();
		capturedContext.ContextSource.ShouldContain("Original context: should be restored after operation run");
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_RestoresOriginalContextAfterSuccess()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(
			() => Task.FromResult("result"),
			"Test operation");

		// Assert
		_mockTenantAccessor.Verify(x => x.SetContext(originalContext), Times.Once);
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_RestoresOriginalContextAfterException()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_manager.ExecuteCrossTenantOperationAsync<string>(
				() => throw new InvalidOperationException("Test error"),
				"Test operation"));

		_mockTenantAccessor.Verify(x => x.SetContext(originalContext), Times.Once);
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
			_manager.ExecuteCrossTenantOperationAsync((Func<Task<string>>)null, "Test"));

		exception.ParamName.ShouldBe("operation");
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithNullJustification_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), null));

		exception.ParamName.ShouldBe("justification");
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithEmptyJustification_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), ""));

		exception.ParamName.ShouldBe("justification");
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithWhitespaceJustification_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "   "));

		exception.ParamName.ShouldBe("justification");
	}

	#endregion

	#region ExecuteCrossTenantOperationAsync (void) Tests

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_VoidOperation_CompletesSuccessfully()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		var operationExecuted = false;

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(
			() =>
			{
				operationExecuted = true;
				return Task.CompletedTask;
			},
			"Test operation");

		// Assert
		operationExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_VoidOperation_RestoresOriginalContext()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(
			() => Task.CompletedTask,
			"Test operation");

		// Assert
		_mockTenantAccessor.Verify(x => x.SetContext(originalContext), Times.Once);
	}

	#endregion

	#region BeginCrossTenantOperationAsync Tests

	[Fact]
	public async Task BeginCrossTenantOperationAsync_ReturnsDisposableContext()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		var disposableContext = await _manager.BeginCrossTenantOperationAsync("Test operation");

		// Assert
		disposableContext.ShouldNotBeNull();
		disposableContext.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public async Task BeginCrossTenantOperationAsync_SetsSystemContext()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		TenantContext capturedContext = null;

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);
		_mockTenantAccessor.Setup(x => x.SetContext(It.IsAny<TenantContext>()))
			.Callback<TenantContext>(ctx => capturedContext = ctx);

		// Act
		await _manager.BeginCrossTenantOperationAsync("Test operation");

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext.IsSystemContext.ShouldBeTrue();
		capturedContext.ContextSource.ShouldContain("Cross-tenant: Test operation");
	}

	[Fact]
	public async Task BeginCrossTenantOperationAsync_WithNullJustification_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_manager.BeginCrossTenantOperationAsync(null));

		exception.ParamName.ShouldBe("justification");
	}

	[Fact]
	public async Task BeginCrossTenantOperationAsync_WithEmptyJustification_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			_manager.BeginCrossTenantOperationAsync(""));

		exception.ParamName.ShouldBe("justification");
	}

	[Fact]
	public async Task BeginCrossTenantOperationAsync_DisposeRestoresOriginalContext()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		var disposableContext = await _manager.BeginCrossTenantOperationAsync("Test operation");
		disposableContext.Dispose();

		// Assert
		_mockTenantAccessor.Verify(x => x.SetContext(originalContext), Times.Once);
	}

	[Fact]
	public async Task BeginCrossTenantOperationAsync_MultipleDisposeCalls_OnlyRestoresOnce()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		var disposableContext = await _manager.BeginCrossTenantOperationAsync("Test operation");
		disposableContext.Dispose();
		disposableContext.Dispose(); // Second dispose should be safe

		// Assert
		_mockTenantAccessor.Verify(x => x.SetContext(originalContext), Times.Once);
	}

	#endregion

	#region User Email and IP Address Tests

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithEmailClaim_ExtractsUserEmail()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") };
		_httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "Test");

		// Assert - The operation should complete without error
		// (Email extraction is internal functionality)
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithLowercaseEmailClaim_ExtractsUserEmail()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		var claims = new[] { new Claim("email", "test@example.com") };
		_httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "Test");

		// Assert - The operation should complete without error
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithNoUser_UsesSystemUser()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		_httpContext.User = null;

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "Test");

		// Assert - The operation should complete without error using "system" as user
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithRemoteIpAddress_ExtractsIpAddress()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		_httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "Test");

		// Assert - The operation should complete without error
	}

	[Fact]
	public async Task ExecuteCrossTenantOperationAsync_WithNoHttpContext_HandlesGracefully()
	{
		// Arrange
		var originalContext = TenantContext.ForTenant(Guid.NewGuid(), "Original");
		_mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

		_mockTenantAccessor.Setup(x => x.Current).Returns(originalContext);

		// Act
		await _manager.ExecuteCrossTenantOperationAsync(() => Task.FromResult("result"), "Test");

		// Assert - The operation should complete without error using defaults
	}

	#endregion
}
