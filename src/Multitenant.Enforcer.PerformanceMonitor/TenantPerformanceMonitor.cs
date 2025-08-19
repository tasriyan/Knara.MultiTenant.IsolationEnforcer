using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;
using System.Diagnostics;

namespace Multitenant.Enforcer.PerformanceMonitor;

public interface ITenantPerformanceMonitor
{
	void RecordQueryExecution(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied);
	Task RecordQueryExecutionAsync(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied);
	void RecordViolation(string violationType, string entityType, string details);
	void RecordCrossTenantOperation(string operation, string justification, TimeSpan executionTime);
	Task<TenantPerformanceStats> GetStatsAsync();
}

public class TenantPerformanceMonitor(
	ILogger<TenantPerformanceMonitor> logger,
	ITenantContextAccessor tenantAccessor,
	IOptions<PerformanceMonitoringOptions> options,
	CurrentUserService currentUserService,
	ITenantMetricsCollector? metricsCollector = null) : ITenantPerformanceMonitor
{
	private readonly ITenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
	private readonly PerformanceMonitoringOptions _options = options.Value;
	private readonly CurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

	public void RecordQueryExecution(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
	{
		var tenantContext = _tenantAccessor.Current;
		var executionTimeMs = (int)executionTime.TotalMilliseconds;

		// Log slow queries
		if (executionTimeMs > _options.SlowQueryThresholdMs)
		{
			logger.LogWarning("Slow query detected: {EntityType}.{QueryType} took {ExecutionTimeMs}ms, returned {RowsReturned} rows, tenant filter: {TenantFilterApplied}, tenant: {TenantId}, user: {UserId}",
				entityType, queryType, executionTimeMs, rowsReturned, tenantFilterApplied, tenantContext.TenantId, _currentUserService.UserId);
		}
		else if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug("Query executed: {EntityType}.{QueryType} took {ExecutionTimeMs}ms, returned {RowsReturned} rows, tenant: {TenantId}, user: {UserId}",
				entityType, queryType, executionTimeMs, rowsReturned, tenantContext.TenantId, _currentUserService.UserId);
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
				RequestId = GetCurrentRequestId(),
				_currentUserService.UserId,
				_currentUserService.UserName,
				_currentUserService.IpAddress,
				_currentUserService.UserAgent,
				_currentUserService.IsAuthenticated
			};

			logger.LogInformation("TenantQueryPerformance: {@PerformanceLog}", performanceLog);
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

		logger.LogCritical("TENANT ISOLATION VIOLATION: Type={ViolationType}, Entity={EntityType}, Tenant={TenantId}, User={UserId}, Details={Details}",
			violationType, entityType, tenantContext.TenantId, _currentUserService.UserId, details);

		var violationLog = new
		{
			ViolationType = violationType,
			EntityType = entityType,
			tenantContext.TenantId,
			Details = details,
			Timestamp = DateTime.UtcNow,
			RequestId = GetCurrentRequestId(),
			_currentUserService.UserId,
			_currentUserService.UserName,
			_currentUserService.UserEmail,
			_currentUserService.UserAgent,
			_currentUserService.IpAddress,
			_currentUserService.IsAuthenticated,
			_currentUserService.UserRoles
		};

		logger.LogCritical("TenantViolation: {@ViolationLog}", violationLog);

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

		logger.LogWarning("Cross-tenant operation executed: Operation={Operation}, Justification={Justification}, ExecutionTime={ExecutionTimeMs}ms, Context={ContextSource}, User={UserId}",
			operation, justification, executionTimeMs, tenantContext.ContextSource, _currentUserService.UserId);

		var crossTenantLog = new
		{
			Operation = operation,
			Justification = justification,
			ExecutionTimeMs = executionTimeMs,
			tenantContext.ContextSource,
			tenantContext.IsSystemContext,
			Timestamp = DateTime.UtcNow,
			RequestId = GetCurrentRequestId(),
			_currentUserService.UserId,
			_currentUserService.UserName,
			_currentUserService.UserEmail,
			_currentUserService.IpAddress,
			_currentUserService.UserAgent,
			_currentUserService.IsAuthenticated,
			_currentUserService.UserRoles
		};

		logger.LogWarning("CrossTenantOperation: {@CrossTenantLog}", crossTenantLog);

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
				Message = "Metrics collection is not enabled",
				AdditionalMetrics = new Dictionary<string, object>
				{
					["CurrentUserId"] = _currentUserService.UserId ?? "unknown",
					["CurrentUserName"] = _currentUserService.UserName ?? "unknown",
					["IsAuthenticated"] = _currentUserService.IsAuthenticated,
					["RequestId"] = GetCurrentRequestId() ?? "unknown"
				}
			};
		}

		var stats = await metricsCollector.GetTenantStatsAsync(_tenantAccessor.Current.TenantId);

		// Enhance stats with current user context
		stats.AdditionalMetrics["CurrentUserId"] = _currentUserService.UserId ?? "unknown";
		stats.AdditionalMetrics["CurrentUserName"] = _currentUserService.UserName ?? "unknown";
		stats.AdditionalMetrics["IsAuthenticated"] = _currentUserService.IsAuthenticated;
		stats.AdditionalMetrics["RequestId"] = GetCurrentRequestId() ?? "unknown";
		stats.AdditionalMetrics["UserRoles"] = _currentUserService.UserRoles;

		return stats;
	}

	private string? GetCurrentRequestId()
	{
		return _currentUserService.RequestId ?? Activity.Current?.Id;
	}
}
