using Microsoft.Extensions.Logging;

namespace Multitenant.Enforcer.PerformanceMonitor;

public class LoggingMetricsCollector(ILogger<LoggingMetricsCollector> logger) : ITenantMetricsCollector
{
	private readonly ILogger<LoggingMetricsCollector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public void RecordQueryMetrics(Guid tenantId, string entityType, string queryType, int executionTimeMs, int rowsReturned)
	{
		var metric = new
		{
			MetricType = "QueryPerformance",
			TenantId = tenantId,
			EntityType = entityType,
			QueryType = queryType,
			ExecutionTimeMs = executionTimeMs,
			RowsReturned = rowsReturned,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("TenantMetric: {@Metric}", metric);
	}

	public void RecordViolation(Guid tenantId, string violationType, string entityType)
	{
		var metric = new
		{
			MetricType = "TenantViolation",
			TenantId = tenantId,
			ViolationType = violationType,
			EntityType = entityType,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogCritical("TenantMetric: {@Metric}", metric);
	}

	public void RecordCrossTenantOperation(string operation, int executionTimeMs)
	{
		var metric = new
		{
			MetricType = "CrossTenantOperation",
			Operation = operation,
			ExecutionTimeMs = executionTimeMs,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogWarning("TenantMetric: {@Metric}", metric);
	}

	public Task FlushMetricsAsync()
	{
		// For logging collector, no async flush needed
		return Task.CompletedTask;
	}

	public Task<TenantPerformanceStats> GetTenantStatsAsync(Guid tenantId)
	{
		// For basic logging collector, return placeholder stats
		return Task.FromResult(new TenantPerformanceStats
		{
			TenantId = tenantId,
			QueryCount = 0,
			AverageQueryTimeMs = 0,
			ViolationCount = 0,
			Message = "Stats available in logs only"
		});
	}
}
