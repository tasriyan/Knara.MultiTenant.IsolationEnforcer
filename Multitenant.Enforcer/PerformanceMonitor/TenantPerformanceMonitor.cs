using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;
using System.Diagnostics;

namespace Multitenant.Enforcer.PerformanceMonitor;

/// <summary>
/// Interface for monitoring tenant-related performance metrics.
/// </summary>
public interface ITenantPerformanceMonitor
{
	/// <summary>
	/// Records a query execution for performance monitoring.
	/// </summary>
	/// <param name="entityType">The entity type being queried</param>
	/// <param name="queryType">The type of query (GetAll, Find, etc.)</param>
	/// <param name="executionTime">Query execution time</param>
	/// <param name="rowsReturned">Number of rows returned</param>
	/// <param name="tenantFilterApplied">Whether tenant filtering was applied</param>
	void RecordQueryExecution(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied);

	/// <summary>
	/// Records a query execution asynchronously.
	/// </summary>
	Task RecordQueryExecutionAsync(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied);

	/// <summary>
	/// Records a tenant isolation violation.
	/// </summary>
	/// <param name="violationType">Type of violation</param>
	/// <param name="entityType">Entity type involved</param>
	/// <param name="details">Additional details</param>
	void RecordViolation(string violationType, string entityType, string details);

	/// <summary>
	/// Records cross-tenant operation execution.
	/// </summary>
	/// <param name="operation">Operation name</param>
	/// <param name="justification">Business justification</param>
	/// <param name="executionTime">Execution time</param>
	void RecordCrossTenantOperation(string operation, string justification, TimeSpan executionTime);

	/// <summary>
	/// Gets performance statistics for the current tenant.
	/// </summary>
	/// <returns>Performance statistics</returns>
	Task<TenantPerformanceStats> GetStatsAsync();
}


public class TenantPerformanceMonitor(
	ITenantContextAccessor tenantAccessor,
	ILogger<TenantPerformanceMonitor> logger,
	IOptions<MultiTenantOptions> options,
	ITenantMetricsCollector? metricsCollector = null) : ITenantPerformanceMonitor
{
	private readonly ITenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
	private readonly ILogger<TenantPerformanceMonitor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly PerformanceMonitoringOptions _options = options.Value.PerformanceMonitoring;

	public void RecordQueryExecution(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
	{
		var tenantContext = _tenantAccessor.Current;
		var executionTimeMs = (int)executionTime.TotalMilliseconds;

		// Log slow queries
		if (executionTimeMs > _options.SlowQueryThresholdMs)
		{
			_logger.LogWarning("Slow query detected: {EntityType}.{QueryType} took {ExecutionTimeMs}ms, returned {RowsReturned} rows, tenant filter: {TenantFilterApplied}, tenant: {TenantId}",
				entityType, queryType, executionTimeMs, rowsReturned, tenantFilterApplied, tenantContext.TenantId);
		}
		else if (_logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("Query executed: {EntityType}.{QueryType} took {ExecutionTimeMs}ms, returned {RowsReturned} rows, tenant: {TenantId}",
				entityType, queryType, executionTimeMs, rowsReturned, tenantContext.TenantId);
		}

		// Record detailed performance metrics
		if (_options.Enabled)
		{
			var performanceLog = new
			{
				EntityType = entityType,
				QueryType = queryType,
				tenantContext.TenantId,
				tenantContext.IsSystemContext,
				ExecutionTimeMs = executionTimeMs,
				RowsReturned = rowsReturned,
				TenantFilterApplied = tenantFilterApplied,
				Timestamp = DateTime.UtcNow,
				RequestId = GetCurrentRequestId()
			};

			_logger.LogInformation("TenantQueryPerformance: {@PerformanceLog}", performanceLog);
		}

		// Send to metrics collector if available
		if (_options.CollectMetrics && metricsCollector != null)
		{
			metricsCollector.RecordQueryMetrics(tenantContext.TenantId, entityType, queryType, executionTimeMs, rowsReturned);
		}
	}

	public async Task RecordQueryExecutionAsync(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
	{
		RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Async operations like sending to external monitoring systems
		if (metricsCollector != null)
		{
			await metricsCollector.FlushMetricsAsync();
		}
	}

	public void RecordViolation(string violationType, string entityType, string details)
	{
		var tenantContext = _tenantAccessor.Current;

		_logger.LogCritical("TENANT ISOLATION VIOLATION: Type={ViolationType}, Entity={EntityType}, Tenant={TenantId}, Details={Details}",
			violationType, entityType, tenantContext.TenantId, details);

		var violationLog = new
		{
			ViolationType = violationType,
			EntityType = entityType,
			tenantContext.TenantId,
			Details = details,
			Timestamp = DateTime.UtcNow,
			RequestId = GetCurrentRequestId(),
			UserAgent = GetCurrentUserAgent(),
			IpAddress = GetCurrentIpAddress()
		};

		_logger.LogCritical("TenantViolation: {@ViolationLog}", violationLog);

		// Send to metrics collector for alerting
		if (metricsCollector != null)
		{
			metricsCollector.RecordViolation(tenantContext.TenantId, violationType, entityType);
		}
	}

	public void RecordCrossTenantOperation(string operation, string justification, TimeSpan executionTime)
	{
		var tenantContext = _tenantAccessor.Current;
		var executionTimeMs = (int)executionTime.TotalMilliseconds;

		_logger.LogWarning("Cross-tenant operation executed: Operation={Operation}, Justification={Justification}, ExecutionTime={ExecutionTimeMs}ms, Context={ContextSource}",
			operation, justification, executionTimeMs, tenantContext.ContextSource);

		var crossTenantLog = new
		{
			Operation = operation,
			Justification = justification,
			ExecutionTimeMs = executionTimeMs,
			tenantContext.ContextSource,
			tenantContext.IsSystemContext,
			Timestamp = DateTime.UtcNow,
			RequestId = GetCurrentRequestId(),
			UserId = GetCurrentUserId()
		};

		_logger.LogWarning("CrossTenantOperation: {@CrossTenantLog}", crossTenantLog);

		if (metricsCollector != null)
		{
			metricsCollector.RecordCrossTenantOperation(operation, executionTimeMs);
		}
	}

	public async Task<TenantPerformanceStats> GetStatsAsync()
	{
		if (metricsCollector == null)
		{
			return new TenantPerformanceStats
			{
				TenantId = _tenantAccessor.Current.TenantId,
				Message = "Metrics collection is not enabled"
			};
		}

		return await metricsCollector.GetTenantStatsAsync(_tenantAccessor.Current.TenantId);
	}

	private static string? GetCurrentRequestId()
	{
		return Activity.Current?.Id;
	}

	private static string? GetCurrentUserAgent()
	{
		// This would need HttpContextAccessor to get actual user agent
		return Activity.Current?.GetBaggageItem("UserAgent");
	}

	private static string? GetCurrentIpAddress()
	{
		// This would need HttpContextAccessor to get actual IP
		return Activity.Current?.GetBaggageItem("ClientIP");
	}

	private static string? GetCurrentUserId()
	{
		// This would need HttpContextAccessor to get actual user ID
		return Activity.Current?.GetBaggageItem("UserId");
	}
}
