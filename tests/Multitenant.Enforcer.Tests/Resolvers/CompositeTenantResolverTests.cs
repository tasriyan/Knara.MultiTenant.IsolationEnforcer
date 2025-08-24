using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.TenantResolvers;
using Multitenant.Enforcer.TenantResolvers.Strategies;
using System.Security.Claims;

namespace Multitenant.Enforcer.Tests.Resolvers;

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
	public async Task ResolveTenantAsync_WithFirstResolverSuccess_ReturnsFirstResult()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var expectedContext = TenantContext.ForTenant(tenantId, "First");
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedContext);

		// Act
		var result = await compositeResolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedContext);
		_mockResolver2.Verify(x => x.GetTenantContextAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithFirstResolverFailSecondSuccess_ReturnsSecondResult()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var expectedContext = TenantContext.ForTenant(tenantId, "Second");
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("First failed", "test", "First"));
		_mockResolver2.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedContext);

		// Act
		var result = await compositeResolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedContext);
		_mockResolver1.Verify(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()), Times.Once);
		_mockResolver2.Verify(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithAllResolversFail_ThrowsCompositeException()
	{
		// Arrange
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object, _mockResolver3.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("First failed", "test1", "First"));
		_mockResolver2.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("Second failed", "test2", "Second"));
		_mockResolver3.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("Third failed", "test3", "Third"));

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			compositeResolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldContain("All tenant resolution strategies failed");
		exception.ResolutionMethod.ShouldBe("Composite");
		exception.AttemptedTenantIdentifier.ShouldBeNull();
	}

	[Fact]
	public async Task ResolveTenantAsync_WithNonTenantResolutionException_PropagatesException()
	{
		// Arrange
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Unexpected error"));

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			compositeResolver.GetTenantContextAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveTenantAsync_WithEmptyResolversArray_ThrowsCompositeException()
	{
		// Arrange
		var context = CreateHttpContext();
		var resolvers = Array.Empty<ITenantResolver>();
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<TenantResolutionException>(() =>
			compositeResolver.GetTenantContextAsync(context, CancellationToken.None));

		exception.Message.ShouldContain("All tenant resolution strategies failed");
		exception.ResolutionMethod.ShouldBe("Composite");
	}

	[Fact]
	public async Task ResolveTenantAsync_PassesCancellationTokenToResolvers()
	{
		// Arrange
		var context = CreateHttpContext();
		var cancellationToken = new CancellationTokenSource().Token;
		var resolvers = new[] { _mockResolver1.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, cancellationToken))
			.ReturnsAsync(TenantContext.ForTenant(Guid.NewGuid(), "Test"));

		// Act
		await compositeResolver.GetTenantContextAsync(context, cancellationToken);

		// Assert
		_mockResolver1.Verify(x => x.GetTenantContextAsync(context, cancellationToken), Times.Once);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithMixedExceptions_ContinuesUntilSuccess()
	{
		// Arrange
		var tenantId = Guid.NewGuid();
		var expectedContext = TenantContext.ForTenant(tenantId, "Success");
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object, _mockResolver3.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("First failed", "test1", "First"));
		_mockResolver2.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TenantResolutionException("Second failed", "test2", "Second"));
		_mockResolver3.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedContext);

		// Act
		var result = await compositeResolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedContext);
		_mockResolver1.Verify(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()), Times.Once);
		_mockResolver2.Verify(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()), Times.Once);
		_mockResolver3.Verify(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ResolveTenantAsync_WithSystemContext_ReturnsSystemContext()
	{
		// Arrange
		var systemContext = TenantContext.SystemContext();
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.ReturnsAsync(systemContext);

		// Act
		var result = await compositeResolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(systemContext);
		result.IsSystemContext.ShouldBeTrue();
	}

	[Fact]
	public async Task ResolveTenantAsync_ExecutesResolversInOrder()
	{
		// Arrange
		var context = CreateHttpContext();
		var resolvers = new[] { _mockResolver1.Object, _mockResolver2.Object, _mockResolver3.Object };
		var compositeResolver = new CompositeTenantResolver(resolvers, _mockLogger.Object);
		var executionOrder = new List<int>();

		_mockResolver1.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.Returns(() =>
			{
				executionOrder.Add(1);
				throw new TenantResolutionException("First failed", "test1", "First");
			});
		_mockResolver2.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.Returns(() =>
			{
				executionOrder.Add(2);
				throw new TenantResolutionException("Second failed", "test2", "Second");
			});
		_mockResolver3.Setup(x => x.GetTenantContextAsync(context, It.IsAny<CancellationToken>()))
			.Returns(() =>
			{
				executionOrder.Add(3);
				return Task.FromResult(TenantContext.ForTenant(Guid.NewGuid(), "Third"));
			});

		// Act
		await compositeResolver.GetTenantContextAsync(context, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(new[] { 1, 2, 3 });
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
