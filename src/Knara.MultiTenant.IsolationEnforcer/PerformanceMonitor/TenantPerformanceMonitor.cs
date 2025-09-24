using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Knara.MultiTenant.IsolationEnforcer.PerformanceMonitor;

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
	ICurrentUserService currentUserService,
	ITenantMetricsCollector? metricsCollector = null) : ITenantPerformanceMonitor
{
	private readonly ITenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
	private readonly PerformanceMonitoringOptions _options = options.Value;
	private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

	public void RecordQueryExecution(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
	{
		// Input validation
		if (string.IsNullOrWhiteSpace(entityType))
		{
			logger.LogWarning("RecordQueryExecution called with null or empty entityType");
			return;
		}

		if (string.IsNullOrWhiteSpace(queryType))
		{
			logger.LogWarning("RecordQueryExecution called with null or empty queryType for entity {EntityType}", entityType);
			return;
		}

		try
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

			// Record detailed performance metrics - only create object if logging is enabled
			if (_options.Enabled && logger.IsEnabled(LogLevel.Information))
			{
				// Cache user properties to avoid multiple property accesses
				var userId = _currentUserService.UserId;
				var userName = _currentUserService.UserName;
				var ipAddress = _currentUserService.IpAddress;
				var userAgent = _currentUserService.UserAgent;
				var isAuthenticated = _currentUserService.IsAuthenticated;
				var requestId = GetCurrentRequestId();

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
					RequestId = requestId,
					UserId = userId,
					UserName = userName,
					IpAddress = ipAddress,
					UserAgent = userAgent,
					IsAuthenticated = isAuthenticated
				};

				logger.LogInformation("TenantQueryPerformance: {@PerformanceLog}", performanceLog);
			}

			// Send to metrics collector if available - protect against exceptions
			if (_options.CollectMetrics && metricsCollector != null)
			{
				try
				{
					metricsCollector.RecordQueryMetrics(tenantContext.TenantId, entityType, queryType, executionTimeMs, rowsReturned);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Failed to record query metrics for {EntityType}.{QueryType}", entityType, queryType);
				}
			}
		}
		catch (Exception ex)
		{
			// Never let monitoring break the application
			logger.LogError(ex, "Failed to record query execution metrics for {EntityType}.{QueryType}", entityType ?? "unknown", queryType ?? "unknown");
		}
	}

	public async Task RecordQueryExecutionAsync(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
	{
		// Record synchronous part first
		RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

		// Async operations like sending to external monitoring systems
		if (metricsCollector != null)
		{
			try
			{
				await metricsCollector.FlushMetricsAsync();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to flush metrics for {EntityType}.{QueryType}", entityType ?? "unknown", queryType ?? "unknown");
				// Don't rethrow - metrics flushing should not break the application
			}
		}
	}

	public void RecordViolation(string violationType, string entityType, string details)
	{
		// Input validation - violations are critical, so we validate strictly
		if (string.IsNullOrWhiteSpace(violationType))
			throw new ArgumentException("Violation type cannot be null or empty", nameof(violationType));

		if (string.IsNullOrWhiteSpace(entityType))
			throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

		try
		{
			var tenantContext = _tenantAccessor.Current;

			logger.LogCritical("TENANT ISOLATION VIOLATION: Type={ViolationType}, Entity={EntityType}, Tenant={TenantId}, User={UserId}, Details={Details}",
				violationType, entityType, tenantContext.TenantId, _currentUserService.UserId, details);

			// Cache user properties to avoid multiple property accesses
			var userId = _currentUserService.UserId;
			var userName = _currentUserService.UserName;
			var userEmail = _currentUserService.UserEmail;
			var userAgent = _currentUserService.UserAgent;
			var ipAddress = _currentUserService.IpAddress;
			var isAuthenticated = _currentUserService.IsAuthenticated;
			var userRoles = _currentUserService.UserRoles;
			var requestId = GetCurrentRequestId();

			var violationLog = new
			{
				ViolationType = violationType,
				EntityType = entityType,
				tenantContext.TenantId,
				Details = details,
				Timestamp = DateTime.UtcNow,
				RequestId = requestId,
				UserId = userId,
				UserName = userName,
				UserEmail = userEmail,
				UserAgent = userAgent,
				IpAddress = ipAddress,
				IsAuthenticated = isAuthenticated,
				UserRoles = userRoles
			};

			logger.LogCritical("TenantViolation: {@ViolationLog}", violationLog);

			// Send to metrics collector for alerting - protect against exceptions
			if (metricsCollector != null)
			{
				try
				{
					metricsCollector.RecordViolation(tenantContext.TenantId, violationType, entityType);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Failed to record violation metrics for {ViolationType} on {EntityType}", violationType, entityType);
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to record violation: {ViolationType} for {EntityType}", violationType, entityType);
			throw;
		}
	}

	public void RecordCrossTenantOperation(string operation, string justification, TimeSpan executionTime)
	{
		if (string.IsNullOrWhiteSpace(operation))
		{
			logger.LogWarning("RecordCrossTenantOperation called with null or empty operation");
			return;
		}

		if (string.IsNullOrWhiteSpace(justification))
		{
			logger.LogWarning("RecordCrossTenantOperation called with null or empty justification for operation {Operation}", operation);
			return;
		}

		try
		{
			var tenantContext = _tenantAccessor.Current;
			var executionTimeMs = (int)executionTime.TotalMilliseconds;

			logger.LogWarning("Cross-tenant operation executed: Operation={Operation}, Justification={Justification}, ExecutionTime={ExecutionTimeMs}ms, Context={ContextSource}, User={UserId}",
				operation, justification, executionTimeMs, tenantContext.ContextSource, _currentUserService.UserId);

			// Cache user properties to avoid multiple property accesses
			var userId = _currentUserService.UserId;
			var userName = _currentUserService.UserName;
			var userEmail = _currentUserService.UserEmail;
			var ipAddress = _currentUserService.IpAddress;
			var userAgent = _currentUserService.UserAgent;
			var isAuthenticated = _currentUserService.IsAuthenticated;
			var userRoles = _currentUserService.UserRoles;
			var requestId = GetCurrentRequestId();

			var crossTenantLog = new
			{
				Operation = operation,
				Justification = justification,
				ExecutionTimeMs = executionTimeMs,
				tenantContext.ContextSource,
				tenantContext.IsSystemContext,
				Timestamp = DateTime.UtcNow,
				RequestId = requestId,
				UserId = userId,
				UserName = userName,
				UserEmail = userEmail,
				IpAddress = ipAddress,
				UserAgent = userAgent,
				IsAuthenticated = isAuthenticated,
				UserRoles = userRoles
			};

			logger.LogWarning("CrossTenantOperation: {@CrossTenantLog}", crossTenantLog);

			// Send to metrics collector - protect against exceptions
			if (metricsCollector != null)
			{
				try
				{
					metricsCollector.RecordCrossTenantOperation(operation, executionTimeMs);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Failed to record cross-tenant operation metrics for {Operation}", operation);
					// Don't rethrow - metrics collection should not break the application
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to record cross-tenant operation: {Operation}", operation ?? "unknown");
			// Don't rethrow - we don't want to break cross-tenant operations due to monitoring failures
		}
	}

	public async Task<TenantPerformanceStats> GetStatsAsync()
	{
		try
		{
			var tenantContext = _tenantAccessor.Current;

			if (metricsCollector == null)
			{
				// Cache user properties
				var userId = _currentUserService.UserId;
				var userName = _currentUserService.UserName;
				var isAuthenticated = _currentUserService.IsAuthenticated;
				var requestId = GetCurrentRequestId();

				return new TenantPerformanceStats
				{
					TenantId = tenantContext.TenantId,
					Message = "Metrics collection is not enabled",
					AdditionalMetrics = new Dictionary<string, object>
					{
						["CurrentUserId"] = userId ?? "unknown",
						["CurrentUserName"] = userName ?? "unknown",
						["IsAuthenticated"] = isAuthenticated,
						["RequestId"] = requestId ?? "unknown"
					}
				};
			}

			var stats = await metricsCollector.GetTenantStatsAsync(tenantContext.TenantId);

			// Cache user properties to avoid multiple property accesses
			var currentUserId = _currentUserService.UserId;
			var currentUserName = _currentUserService.UserName;
			var currentIsAuthenticated = _currentUserService.IsAuthenticated;
			var currentRequestId = GetCurrentRequestId();
			var currentUserRoles = _currentUserService.UserRoles;

			// Enhance stats with current user context
			stats.AdditionalMetrics["CurrentUserId"] = currentUserId ?? "unknown";
			stats.AdditionalMetrics["CurrentUserName"] = currentUserName ?? "unknown";
			stats.AdditionalMetrics["IsAuthenticated"] = currentIsAuthenticated;
			stats.AdditionalMetrics["RequestId"] = currentRequestId ?? "unknown";
			stats.AdditionalMetrics["UserRoles"] = currentUserRoles;

			return stats;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to get tenant performance stats");

			// Return a fallback stats object instead of letting the exception bubble up
			try
			{
				var tenantId = _tenantAccessor.Current?.TenantId ?? Guid.Empty;
				return new TenantPerformanceStats
				{
					TenantId = tenantId,
					Message = $"Failed to retrieve stats: {ex.Message}",
					AdditionalMetrics = new Dictionary<string, object>
					{
						["Error"] = ex.Message,
						["Timestamp"] = DateTime.UtcNow
					}
				};
			}
			catch
			{
				// If we can't even get tenant context, return minimal stats
				return new TenantPerformanceStats
				{
					TenantId = Guid.Empty,
					Message = "Failed to retrieve stats and tenant context",
					AdditionalMetrics = new Dictionary<string, object>
					{
						["Error"] = ex.Message,
						["Timestamp"] = DateTime.UtcNow
					}
				};
			}
		}
	}

	private string? GetCurrentRequestId()
	{
		try
		{
			return _currentUserService.RequestId ?? Activity.Current?.Id;
		}
		catch
		{
			// If there's any issue getting the request ID, return null
			return null;
		}
	}
}
