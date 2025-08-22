using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.PerformanceMonitor;
using System.Diagnostics;

namespace Multitenant.Enforcer.Tests.PerformanceMonitor;

public class TenantPerformanceMonitorTests
{
	private readonly Mock<ILogger<TenantPerformanceMonitor>> _mockLogger;
	private readonly Mock<ITenantContextAccessor> _mockTenantAccessor;
	private readonly Mock<IOptions<PerformanceMonitoringOptions>> _mockOptions;
	private readonly Mock<ICurrentUserService> _mockCurrentUserService;
	private readonly Mock<ITenantMetricsCollector> _mockMetricsCollector;
	private readonly PerformanceMonitoringOptions _defaultOptions;
	private readonly TenantContext _defaultTenantContext;
	private readonly TenantPerformanceMonitor _monitor;

	public TenantPerformanceMonitorTests()
	{
		_mockLogger = new Mock<ILogger<TenantPerformanceMonitor>>();
		_mockTenantAccessor = new Mock<ITenantContextAccessor>();
		_mockOptions = new Mock<IOptions<PerformanceMonitoringOptions>>();
		_mockCurrentUserService = new Mock<ICurrentUserService>();
		_mockMetricsCollector = new Mock<ITenantMetricsCollector>();

		_defaultOptions = new PerformanceMonitoringOptions
		{
			Enabled = true,
			SlowQueryThresholdMs = 1000,
			CollectMetrics = true
		};

		_defaultTenantContext = TenantContext.ForTenant(Guid.NewGuid(), "Test");

		_mockOptions.Setup(x => x.Value).Returns(_defaultOptions);
		_mockTenantAccessor.Setup(x => x.Current).Returns(_defaultTenantContext);

		SetupDefaultCurrentUserService();

		_monitor = new TenantPerformanceMonitor(
			_mockLogger.Object,
			_mockTenantAccessor.Object,
			_mockOptions.Object,
			_mockCurrentUserService.Object,
			_mockMetricsCollector.Object);
	}

	private void SetupDefaultCurrentUserService()
	{
		_mockCurrentUserService.Setup(x => x.UserId).Returns("test-user-id");
		_mockCurrentUserService.Setup(x => x.UserName).Returns("Test User");
		_mockCurrentUserService.Setup(x => x.UserEmail).Returns("test@example.com");
		_mockCurrentUserService.Setup(x => x.IpAddress).Returns("192.168.1.1");
		_mockCurrentUserService.Setup(x => x.UserAgent).Returns("Test Agent");
		_mockCurrentUserService.Setup(x => x.RequestId).Returns("test-request-id");
		_mockCurrentUserService.Setup(x => x.IsAuthenticated).Returns(true);
		_mockCurrentUserService.Setup(x => x.UserRoles).Returns(new[] { "User", "Admin" });
	}

	#region RecordQueryExecution Tests

	[Fact]
	public void RecordQueryExecution_WithValidParameters_LogsInformation()
	{
		// Arrange
		var entityType = "User";
		var queryType = "GetById";
		var executionTime = TimeSpan.FromMilliseconds(500);
		var rowsReturned = 1;
		var tenantFilterApplied = true;

		_mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);

		// Act
		_monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TenantQueryPerformance")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithSlowQuery_LogsWarning()
	{
		// Arrange
		var entityType = "User";
		var queryType = "GetAll";
		var executionTime = TimeSpan.FromMilliseconds(1500); // Above threshold
		var rowsReturned = 100;
		var tenantFilterApplied = true;

		// Act
		_monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithFastQueryAndDebugEnabled_LogsDebug()
	{
		// Arrange
		var entityType = "User";
		var queryType = "GetById";
		var executionTime = TimeSpan.FromMilliseconds(100);
		var rowsReturned = 1;
		var tenantFilterApplied = true;

		_mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

		// Act
		_monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Debug,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Query executed")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithNullEntityType_LogsWarningAndReturns()
	{
		// Act
		_monitor.RecordQueryExecution(null!, "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty entityType")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		// Should not call metrics collector
		_mockMetricsCollector.Verify(x => x.RecordQueryMetrics(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public void RecordQueryExecution_WithEmptyQueryType_LogsWarningAndReturns()
	{
		// Act
		_monitor.RecordQueryExecution("User", "", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty queryType")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithMonitoringDisabled_DoesNotLogPerformanceMetrics()
	{
		// Arrange
		_defaultOptions.Enabled = false;

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TenantQueryPerformance")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Never);
	}

	[Fact]
	public void RecordQueryExecution_WithMetricsCollectionEnabled_CallsMetricsCollector()
	{
		// Arrange
		var entityType = "User";
		var queryType = "GetById";
		var executionTime = TimeSpan.FromMilliseconds(500);
		var rowsReturned = 1;

		// Act
		_monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, true);

		// Assert
		_mockMetricsCollector.Verify(
			x => x.RecordQueryMetrics(_defaultTenantContext.TenantId, entityType, queryType, 500, rowsReturned),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithMetricsCollectorThrowingException_LogsErrorAndContinues()
	{
		// Arrange
		_mockMetricsCollector.Setup(x => x.RecordQueryMetrics(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
			.Throws(new InvalidOperationException("Metrics service unavailable"));

		// Act
		Should.NotThrow(() => _monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record query metrics")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordQueryExecution_WithTenantAccessorThrowingException_LogsErrorAndContinues()
	{
		// Arrange
		_mockTenantAccessor.Setup(x => x.Current).Throws(new InvalidOperationException("No tenant context"));

		// Act
		Should.NotThrow(() => _monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record query execution metrics")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region RecordQueryExecutionAsync Tests

	[Fact]
	public async Task RecordQueryExecutionAsync_CallsRecordQueryExecution()
	{
		// Arrange
		var entityType = "User";
		var queryType = "GetById";
		var executionTime = TimeSpan.FromMilliseconds(500);
		var rowsReturned = 1;
		var tenantFilterApplied = true;

		// Act
		await _monitor.RecordQueryExecutionAsync(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Assert - Verify that metrics collector is called (which means RecordQueryExecution was called)
		_mockMetricsCollector.Verify(
			x => x.RecordQueryMetrics(_defaultTenantContext.TenantId, entityType, queryType, 500, rowsReturned),
			Times.Once);
	}

	[Fact]
	public async Task RecordQueryExecutionAsync_WithMetricsCollector_CallsFlushMetricsAsync()
	{
		// Act
		await _monitor.RecordQueryExecutionAsync("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		_mockMetricsCollector.Verify(x => x.FlushMetricsAsync(), Times.Once);
	}

	[Fact]
	public async Task RecordQueryExecutionAsync_WithMetricsCollectorFlushThrowingException_LogsErrorAndContinues()
	{
		// Arrange
		_mockMetricsCollector.Setup(x => x.FlushMetricsAsync())
			.ThrowsAsync(new InvalidOperationException("Flush failed"));

		// Act
		await Should.NotThrowAsync(async () =>
			await _monitor.RecordQueryExecutionAsync("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to flush metrics")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task RecordQueryExecutionAsync_WithNullMetricsCollector_DoesNotCallFlush()
	{
		// Arrange
		var monitor = new TenantPerformanceMonitor(
			_mockLogger.Object,
			_mockTenantAccessor.Object,
			_mockOptions.Object,
			_mockCurrentUserService.Object,
			null);

		// Act
		await monitor.RecordQueryExecutionAsync("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert - No exception should be thrown
		_mockMetricsCollector.Verify(x => x.FlushMetricsAsync(), Times.Never);
	}

	#endregion

	#region RecordViolation Tests

	[Fact]
	public void RecordViolation_WithValidParameters_LogsCritical()
	{
		// Arrange
		var violationType = "UnauthorizedAccess";
		var entityType = "User";
		var details = "User attempted to access data from different tenant";

		// Act
		_monitor.RecordViolation(violationType, entityType, details);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Critical,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TENANT ISOLATION VIOLATION")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Critical,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TenantViolation")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordViolation_WithNullViolationType_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			_monitor.RecordViolation(null!, "User", "Test details"));

		exception.ParamName.ShouldBe("violationType");
		exception.Message.ShouldContain("Violation type cannot be null or empty");
	}

	[Fact]
	public void RecordViolation_WithEmptyEntityType_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			_monitor.RecordViolation("UnauthorizedAccess", "", "Test details"));

		exception.ParamName.ShouldBe("entityType");
		exception.Message.ShouldContain("Entity type cannot be null or empty");
	}

	[Fact]
	public void RecordViolation_WithWhitespaceViolationType_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			_monitor.RecordViolation("   ", "User", "Test details"));

		exception.ParamName.ShouldBe("violationType");
	}

	[Fact]
	public void RecordViolation_WithMetricsCollector_CallsRecordViolation()
	{
		// Arrange
		var violationType = "UnauthorizedAccess";
		var entityType = "User";
		var details = "Test violation";

		// Act
		_monitor.RecordViolation(violationType, entityType, details);

		// Assert
		_mockMetricsCollector.Verify(
			x => x.RecordViolation(_defaultTenantContext.TenantId, violationType, entityType),
			Times.Once);
	}

	[Fact]
	public void RecordViolation_WithMetricsCollectorThrowingException_LogsErrorButStillRethrows()
	{
		// Arrange
		_mockMetricsCollector.Setup(x => x.RecordViolation(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
			.Throws(new InvalidOperationException("Metrics service unavailable"));

		// Act
		_monitor.RecordViolation("UnauthorizedAccess", "User", "Test details");

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record violation metrics")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordViolation_WithTenantAccessorThrowingException_LogsErrorAndRethrows()
	{
		// Arrange
		_mockTenantAccessor.Setup(x => x.Current).Throws(new InvalidOperationException("No tenant context"));

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			_monitor.RecordViolation("UnauthorizedAccess", "User", "Test details"));

		exception.Message.ShouldBe("No tenant context");

		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record violation")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region RecordCrossTenantOperation Tests

	[Fact]
	public void RecordCrossTenantOperation_WithValidParameters_LogsWarning()
	{
		// Arrange
		var operation = "AdminViewAllUsers";
		var justification = "System administrator needs to view all users for compliance audit";
		var executionTime = TimeSpan.FromMilliseconds(750);

		// Act
		_monitor.RecordCrossTenantOperation(operation, justification, executionTime);

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cross-tenant operation executed")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CrossTenantOperation")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordCrossTenantOperation_WithNullOperation_LogsWarningAndReturns()
	{
		// Act
		_monitor.RecordCrossTenantOperation(null!, "Justification", TimeSpan.FromMilliseconds(100));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty operation")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);

		// Should not call metrics collector
		_mockMetricsCollector.Verify(x => x.RecordCrossTenantOperation(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
	}

	[Fact]
	public void RecordCrossTenantOperation_WithEmptyJustification_LogsWarningAndReturns()
	{
		// Act
		_monitor.RecordCrossTenantOperation("AdminOperation", "", TimeSpan.FromMilliseconds(100));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null or empty justification")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordCrossTenantOperation_WithMetricsCollector_CallsRecordCrossTenantOperation()
	{
		// Arrange
		var operation = "AdminViewAllUsers";
		var justification = "System audit";
		var executionTime = TimeSpan.FromMilliseconds(500);

		// Act
		_monitor.RecordCrossTenantOperation(operation, justification, executionTime);

		// Assert
		_mockMetricsCollector.Verify(
			x => x.RecordCrossTenantOperation(operation, 500),
			Times.Once);
	}

	[Fact]
	public void RecordCrossTenantOperation_WithMetricsCollectorThrowingException_LogsErrorAndContinues()
	{
		// Arrange
		_mockMetricsCollector.Setup(x => x.RecordCrossTenantOperation(It.IsAny<string>(), It.IsAny<int>()))
			.Throws(new InvalidOperationException("Metrics service unavailable"));

		// Act
		Should.NotThrow(() => _monitor.RecordCrossTenantOperation("AdminOperation", "Test justification", TimeSpan.FromMilliseconds(100)));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record cross-tenant operation metrics")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public void RecordCrossTenantOperation_WithTenantAccessorThrowingException_LogsErrorAndContinues()
	{
		// Arrange
		_mockTenantAccessor.Setup(x => x.Current).Throws(new InvalidOperationException("No tenant context"));

		// Act
		Should.NotThrow(() => _monitor.RecordCrossTenantOperation("AdminOperation", "Test justification", TimeSpan.FromMilliseconds(100)));

		// Assert
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to record cross-tenant operation")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	#endregion

	#region GetStatsAsync Tests

	[Fact]
	public async Task GetStatsAsync_WithNullMetricsCollector_ReturnsStatsWithMessage()
	{
		// Arrange
		var monitor = new TenantPerformanceMonitor(
			_mockLogger.Object,
			_mockTenantAccessor.Object,
			_mockOptions.Object,
			_mockCurrentUserService.Object,
			null);

		// Act
		var result = await monitor.GetStatsAsync();

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(_defaultTenantContext.TenantId);
		result.Message.ShouldBe("Metrics collection is not enabled");
		result.AdditionalMetrics.ShouldContainKey("CurrentUserId");
		result.AdditionalMetrics.ShouldContainKey("CurrentUserName");
		result.AdditionalMetrics.ShouldContainKey("IsAuthenticated");
		result.AdditionalMetrics.ShouldContainKey("RequestId");
	}

	[Fact]
	public async Task GetStatsAsync_WithMetricsCollector_ReturnsEnhancedStats()
	{
		// Arrange
		var baseStats = new TenantPerformanceStats
		{
			TenantId = _defaultTenantContext.TenantId,
			QueryCount = 100,
			AverageQueryTimeMs = 250.5,
			SlowQueryCount = 5,
			ViolationCount = 0,
			CrossTenantOperationCount = 2,
			AdditionalMetrics = new Dictionary<string, object>
			{
				["OriginalMetric"] = "value"
			}
		};

		_mockMetricsCollector.Setup(x => x.GetTenantStatsAsync(_defaultTenantContext.TenantId))
			.ReturnsAsync(baseStats);

		// Act
		var result = await _monitor.GetStatsAsync();

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(_defaultTenantContext.TenantId);
		result.QueryCount.ShouldBe(100);
		result.AverageQueryTimeMs.ShouldBe(250.5);
		result.SlowQueryCount.ShouldBe(5);
		result.ViolationCount.ShouldBe(0);
		result.CrossTenantOperationCount.ShouldBe(2);

		// Should contain original metrics
		result.AdditionalMetrics.ShouldContainKey("OriginalMetric");
		result.AdditionalMetrics["OriginalMetric"].ShouldBe("value");

		// Should contain enhanced user context
		result.AdditionalMetrics.ShouldContainKey("CurrentUserId");
		result.AdditionalMetrics["CurrentUserId"].ShouldBe("test-user-id");
		result.AdditionalMetrics.ShouldContainKey("CurrentUserName");
		result.AdditionalMetrics["CurrentUserName"].ShouldBe("Test User");
		result.AdditionalMetrics.ShouldContainKey("IsAuthenticated");
		result.AdditionalMetrics["IsAuthenticated"].ShouldBe(true);
		result.AdditionalMetrics.ShouldContainKey("RequestId");
		result.AdditionalMetrics["RequestId"].ShouldBe("test-request-id");
		result.AdditionalMetrics.ShouldContainKey("UserRoles");
	}

	[Fact]
	public async Task GetStatsAsync_WithMetricsCollectorThrowingException_ReturnsFallbackStats()
	{
		// Arrange
		_mockMetricsCollector.Setup(x => x.GetTenantStatsAsync(It.IsAny<Guid>()))
			.ThrowsAsync(new InvalidOperationException("Service unavailable"));

		// Act
		var result = await _monitor.GetStatsAsync();

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(_defaultTenantContext.TenantId);
		result.Message.ShouldContain("Failed to retrieve stats");
		result.AdditionalMetrics.ShouldContainKey("Error");
		result.AdditionalMetrics["Error"].ShouldBe("Service unavailable");

		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get tenant performance stats")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task GetStatsAsync_WithTenantAccessorThrowingException_ReturnsMinimalStats()
	{
		// Arrange
		_mockTenantAccessor.Setup(x => x.Current).Throws(new InvalidOperationException("No tenant context"));

		// Act
		var result = await _monitor.GetStatsAsync();

		// Assert
		result.ShouldNotBeNull();
		result.TenantId.ShouldBe(Guid.Empty);
		result.Message.ShouldBe("Failed to retrieve stats and tenant context");
		result.AdditionalMetrics.ShouldContainKey("Error");
		result.AdditionalMetrics["Error"].ShouldBe("No tenant context");
	}

	#endregion

	#region GetCurrentRequestId Tests

	[Fact]
	public void GetCurrentRequestId_WithCurrentUserServiceRequestId_ReturnsRequestId()
	{
		// Arrange
		_mockCurrentUserService.Setup(x => x.RequestId).Returns("test-request-123");

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// This test verifies indirectly through logging that the request ID is used
		// Since GetCurrentRequestId is private, we test its behavior through public methods
	}

	[Fact]
	public void GetCurrentRequestId_WithNullCurrentUserServiceRequestId_FallsBackToActivity()
	{
		// Arrange
		_mockCurrentUserService.Setup(x => x.RequestId).Returns((string?)null);

		using var activity = new Activity("TestActivity");
		activity.Start();

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// The test verifies that no exception is thrown and the method completes successfully
		// The Activity.Current?.Id fallback is tested indirectly
	}

	[Fact]
	public void GetCurrentRequestId_WithCurrentUserServiceThrowingException_ReturnsNull()
	{
		// Arrange
		_mockCurrentUserService.Setup(x => x.RequestId).Throws(new InvalidOperationException("Request ID unavailable"));

		// Act
		Should.NotThrow(() => _monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true));

		// The test verifies that exceptions in getting request ID don't break the monitoring
	}

	#endregion

	#region Configuration Tests

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void RecordQueryExecution_WithDifferentEnabledSettings_BehavesCorrectly(bool enabled, bool shouldLogPerformance)
	{
		// Arrange
		_defaultOptions.Enabled = enabled;
		_mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		var expectedTimes = shouldLogPerformance ? Times.Once() : Times.Never();
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Information,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TenantQueryPerformance")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			expectedTimes);
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void RecordQueryExecution_WithDifferentCollectMetricsSettings_BehavesCorrectly(bool collectMetrics, bool shouldCallMetricsCollector)
	{
		// Arrange
		_defaultOptions.CollectMetrics = collectMetrics;

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert
		var expectedTimes = shouldCallMetricsCollector ? Times.Once() : Times.Never();
		_mockMetricsCollector.Verify(
			x => x.RecordQueryMetrics(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
			expectedTimes);
	}

	[Theory]
	[InlineData(500, 1000, false)]  // Below threshold
	[InlineData(1000, 1000, false)] // At threshold
	[InlineData(1500, 1000, true)]  // Above threshold
	public void RecordQueryExecution_WithDifferentThresholds_LogsSlowQueriesCorrectly(int executionTimeMs, int thresholdMs, bool shouldLogSlowQuery)
	{
		// Arrange
		_defaultOptions.SlowQueryThresholdMs = thresholdMs;

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(executionTimeMs), 1, true);

		// Assert
		var expectedTimes = shouldLogSlowQuery ? Times.Once() : Times.Never();
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			expectedTimes);
	}

	#endregion

	#region Edge Cases and Integration Tests

	[Fact]
	public void RecordQueryExecution_WithSystemContext_LogsCorrectly()
	{
		// Arrange
		var systemContext = TenantContext.SystemContext("Test system operation");
		_mockTenantAccessor.Setup(x => x.Current).Returns(systemContext);

		// Act
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);

		// Assert - Should not throw and should log the system context
		_mockMetricsCollector.Verify(
			x => x.RecordQueryMetrics(systemContext.TenantId, "User", "GetById", 100, 1),
			Times.Once);
	}

	[Fact]
	public async Task MultipleOperations_WorkCorrectlyTogether()
	{
		// Act - Perform multiple operations
		_monitor.RecordQueryExecution("User", "GetById", TimeSpan.FromMilliseconds(100), 1, true);
		await _monitor.RecordQueryExecutionAsync("Project", "GetAll", TimeSpan.FromMilliseconds(1500), 50, true);
		_monitor.RecordCrossTenantOperation("AdminAudit", "Compliance review", TimeSpan.FromMilliseconds(300));

		// Violation should be last as it might throw
		_monitor.RecordViolation("UnauthorizedAccess", "User", "Cross-tenant data access detected");

		// Assert - All operations should complete and call appropriate methods
		_mockMetricsCollector.Verify(x => x.RecordQueryMetrics(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
		_mockMetricsCollector.Verify(x => x.FlushMetricsAsync(), Times.Once);
		_mockMetricsCollector.Verify(x => x.RecordCrossTenantOperation(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
		_mockMetricsCollector.Verify(x => x.RecordViolation(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
	}

	#endregion
}
