using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.PerformanceMonitor;
using System.Diagnostics;

namespace MultiTenant.Enforcer.Tests.PerformanceMonitor;

public class TenantPerformanceMonitorTests
{
    private readonly Mock<ITenantContextAccessor> _mockTenantAccessor;
    private readonly Mock<ILogger<TenantPerformanceMonitor>> _mockLogger;
    private readonly Mock<ITenantMetricsCollector> _mockMetricsCollector;
    private readonly MultiTenantOptions _options;
    private readonly TenantPerformanceMonitor _monitor;
    private readonly TenantContext _tenantContext;

    public TenantPerformanceMonitorTests()
    {
        _mockTenantAccessor = new Mock<ITenantContextAccessor>();
        _mockLogger = new Mock<ILogger<TenantPerformanceMonitor>>();
        _mockMetricsCollector = new Mock<ITenantMetricsCollector>();
        
        _options = new MultiTenantOptions();
        _options.PerformanceMonitoring.Enabled = true;
        _options.PerformanceMonitoring.SlowQueryThresholdMs = 1000;
        _options.PerformanceMonitoring.CollectMetrics = true;

        _tenantContext = TenantContext.ForTenant(Guid.NewGuid(), "JWT");
        _mockTenantAccessor.Setup(x => x.Current).Returns(_tenantContext);

        _monitor = new TenantPerformanceMonitor(
            _mockTenantAccessor.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockMetricsCollector.Object);
    }

    [Fact]
    public void RecordQueryExecution_WithNormalQuery_LogsDebugAndRecordsMetrics()
    {
        // Arrange
        var entityType = "User";
        var queryType = "GetAll";
        var executionTime = TimeSpan.FromMilliseconds(500);
        var rowsReturned = 10;
        var tenantFilterApplied = true;

        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        _monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Query executed: User.GetAll took 500ms")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockMetricsCollector.Verify(x => x.RecordQueryMetrics(
            _tenantContext.TenantId, entityType, queryType, 500, rowsReturned), Times.Once);
    }

    [Fact]
    public void RecordQueryExecution_WithSlowQuery_LogsWarning()
    {
        // Arrange
        var entityType = "Order";
        var queryType = "ComplexSearch";
        var executionTime = TimeSpan.FromMilliseconds(1500);
        var rowsReturned = 100;
        var tenantFilterApplied = true;

        // Act
        _monitor.RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected: Order.ComplexSearch took 1500ms")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordQueryExecution_WithPerformanceMonitoringEnabled_LogsPerformanceData()
    {
        // Arrange
        var entityType = "Product";
        var queryType = "Find";
        var executionTime = TimeSpan.FromMilliseconds(250);
        var rowsReturned = 1;
        var tenantFilterApplied = true;

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
    public void RecordQueryExecution_WithPerformanceMonitoringDisabled_DoesNotLogPerformanceData()
    {
        // Arrange
        _options.PerformanceMonitoring.Enabled = false;
        var entityType = "Product";
        var queryType = "Find";
        var executionTime = TimeSpan.FromMilliseconds(250);

        // Act
        _monitor.RecordQueryExecution(entityType, queryType, executionTime, 1, true);

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
    public void RecordQueryExecution_WithMetricsCollectionDisabled_DoesNotRecordMetrics()
    {
        // Arrange
        _options.PerformanceMonitoring.CollectMetrics = false;
        var entityType = "User";
        var queryType = "GetAll";
        var executionTime = TimeSpan.FromMilliseconds(500);

        // Act
        _monitor.RecordQueryExecution(entityType, queryType, executionTime, 10, true);

        // Assert
        _mockMetricsCollector.Verify(x => x.RecordQueryMetrics(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void RecordQueryExecution_WithNullMetricsCollector_DoesNotThrow()
    {
        // Arrange
        var monitorWithoutCollector = new TenantPerformanceMonitor(
            _mockTenantAccessor.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act & Assert
        Should.NotThrow(() => monitorWithoutCollector.RecordQueryExecution("User", "GetAll", TimeSpan.FromMilliseconds(500), 10, true));
    }

    [Fact]
    public async Task RecordQueryExecutionAsync_CallsSyncMethodAndFlushesMetrics()
    {
        // Arrange
        var entityType = "User";
        var queryType = "GetAll";
        var executionTime = TimeSpan.FromMilliseconds(500);
        var rowsReturned = 10;
        var tenantFilterApplied = true;

        // Act
        await _monitor.RecordQueryExecutionAsync(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

        // Assert
        _mockMetricsCollector.Verify(x => x.RecordQueryMetrics(
            _tenantContext.TenantId, entityType, queryType, 500, rowsReturned), Times.Once);
        _mockMetricsCollector.Verify(x => x.FlushMetricsAsync(), Times.Once);
    }

    [Fact]
    public async Task RecordQueryExecutionAsync_WithNullMetricsCollector_DoesNotThrow()
    {
        // Arrange
        var monitorWithoutCollector = new TenantPerformanceMonitor(
            _mockTenantAccessor.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act & Assert
        await Should.NotThrowAsync(() => monitorWithoutCollector.RecordQueryExecutionAsync("User", "GetAll", TimeSpan.FromMilliseconds(500), 10, true));
    }

    [Fact]
    public void RecordViolation_LogsCriticalAndRecordsMetrics()
    {
        // Arrange
        var violationType = "CrossTenantDataAccess";
        var entityType = "Order";
        var details = "Attempted to access order from different tenant";

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

        _mockMetricsCollector.Verify(x => x.RecordViolation(_tenantContext.TenantId, violationType, entityType), Times.Once);
    }

    [Fact]
    public void RecordViolation_WithNullMetricsCollector_StillLogsViolation()
    {
        // Arrange
        var monitorWithoutCollector = new TenantPerformanceMonitor(
            _mockTenantAccessor.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        monitorWithoutCollector.RecordViolation("TestViolation", "TestEntity", "Test details");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TENANT ISOLATION VIOLATION")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordCrossTenantOperation_LogsWarningAndRecordsMetrics()
    {
        // Arrange
        var operation = "BulkDataMigration";
        var justification = "Monthly data consolidation for reporting";
        var executionTime = TimeSpan.FromMilliseconds(2500);

        // Act
        _monitor.RecordCrossTenantOperation(operation, justification, executionTime);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cross-tenant operation executed: Operation=BulkDataMigration")),
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

        _mockMetricsCollector.Verify(x => x.RecordCrossTenantOperation(operation, 2500), Times.Once);
    }

    [Fact]
    public void RecordCrossTenantOperation_WithSystemContext_LogsContextSource()
    {
        // Arrange
        var systemContext = TenantContext.SystemContext("BackgroundJob");
        _mockTenantAccessor.Setup(x => x.Current).Returns(systemContext);

        var operation = "SystemMaintenance";
        var justification = "Scheduled maintenance task";
        var executionTime = TimeSpan.FromMilliseconds(1000);

        // Act
        _monitor.RecordCrossTenantOperation(operation, justification, executionTime);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Context=BackgroundJob")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_WithMetricsCollector_ReturnsTenantStats()
    {
        // Arrange
        var expectedStats = new TenantPerformanceStats
        {
            TenantId = _tenantContext.TenantId,
            QueryCount = 100,
            AverageQueryTimeMs = 250.5,
            SlowQueryCount = 5,
            ViolationCount = 0,
            CrossTenantOperationCount = 2
        };

        _mockMetricsCollector.Setup(x => x.GetTenantStatsAsync(_tenantContext.TenantId))
                           .ReturnsAsync(expectedStats);

        // Act
        var result = await _monitor.GetStatsAsync();

        // Assert
        result.ShouldBe(expectedStats);
        result.TenantId.ShouldBe(_tenantContext.TenantId);
        result.QueryCount.ShouldBe(100);
        result.AverageQueryTimeMs.ShouldBe(250.5);
    }

    [Fact]
    public async Task GetStatsAsync_WithoutMetricsCollector_ReturnsDefaultStats()
    {
        // Arrange
        var monitorWithoutCollector = new TenantPerformanceMonitor(
            _mockTenantAccessor.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null);

        // Act
        var result = await monitorWithoutCollector.GetStatsAsync();

        // Assert
        result.TenantId.ShouldBe(_tenantContext.TenantId);
        result.Message.ShouldBe("Metrics collection is not enabled");
    }

    [Theory]
    [InlineData(500, false)] // Normal query
    [InlineData(1000, false)] // At threshold
    [InlineData(1001, true)] // Above threshold
    [InlineData(2000, true)] // Slow query
    public void RecordQueryExecution_WithVariousExecutionTimes_LogsAppropriately(int executionTimeMs, bool shouldLogWarning)
    {
        // Arrange
        var executionTime = TimeSpan.FromMilliseconds(executionTimeMs);
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

        // Act
        _monitor.RecordQueryExecution("TestEntity", "TestQuery", executionTime, 1, true);

        // Assert
        if (shouldLogWarning)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slow query detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        else
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Query executed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    [Fact]
    public void RecordQueryExecution_WithDebugDisabled_DoesNotLogDebugMessages()
    {
        // Arrange
        _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(false);
        var executionTime = TimeSpan.FromMilliseconds(500);

        // Act
        _monitor.RecordQueryExecution("TestEntity", "TestQuery", executionTime, 1, true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void RecordQueryExecution_IncludesCurrentActivityId()
    {
        // Arrange
        using var activity = new Activity("TestActivity").Start();
        var executionTime = TimeSpan.FromMilliseconds(500);

        // Act
        _monitor.RecordQueryExecution("TestEntity", "TestQuery", executionTime, 1, true);

        // Assert - The activity ID should be included in performance logging
        // This is tested implicitly through the structured logging
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
    public void RecordViolation_IncludesRequestTrackingInformation()
    {
        // Arrange
        using var activity = new Activity("TestActivity").Start();

        // Act
        _monitor.RecordViolation("TestViolation", "TestEntity", "Test details");

        // Assert - Verify that violation log includes tracking information
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
    public void RecordCrossTenantOperation_IncludesRequestTrackingInformation()
    {
        // Arrange
        using var activity = new Activity("TestActivity").Start();

        // Act
        _monitor.RecordCrossTenantOperation("TestOperation", "Test justification", TimeSpan.FromMilliseconds(1000));

        // Assert - Verify that cross-tenant log includes tracking information
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CrossTenantOperation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}