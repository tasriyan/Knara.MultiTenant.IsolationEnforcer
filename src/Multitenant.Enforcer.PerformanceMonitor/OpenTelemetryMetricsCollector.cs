using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Multitenant.Enforcer.PerformanceMonitor;

public class OpenTelemetryMetricsCollector : ITenantMetricsCollector, IDisposable
{
	private readonly Meter _meter;
	private readonly Counter<int> _queryCounter;
	private readonly Histogram<int> _queryDurationHistogram;
	private readonly Counter<int> _rowsReturnedCounter;
	private readonly Counter<int> _violationCounter;
	private readonly Counter<int> _crossTenantOperationCounter;
	private readonly Histogram<int> _crossTenantOperationDurationHistogram;
	
	// In-memory stats for GetTenantStatsAsync - in production, consider using a proper storage backend
	private readonly ConcurrentDictionary<Guid, TenantMetricsState> _tenantStats;
	private readonly object _statsLock = new();

	public OpenTelemetryMetricsCollector(string? meterName = null)
	{
		var name = meterName ?? "Multitenant.Enforcer.PerformanceMonitor";
		_meter = new Meter(name, "1.0.0");
		
		// Initialize metrics instruments
		_queryCounter = _meter.CreateCounter<int>(
			"tenant_queries_total",
			"count",
			"Total number of tenant queries executed");
			
		_queryDurationHistogram = _meter.CreateHistogram<int>(
			"tenant_query_duration_ms",
			"milliseconds", 
			"Duration of tenant queries in milliseconds");
			
		_rowsReturnedCounter = _meter.CreateCounter<int>(
			"tenant_query_rows_returned_total",
			"count",
			"Total number of rows returned by tenant queries");
			
		_violationCounter = _meter.CreateCounter<int>(
			"tenant_violations_total",
			"count",
			"Total number of tenant isolation violations");
			
		_crossTenantOperationCounter = _meter.CreateCounter<int>(
			"cross_tenant_operations_total",
			"count",
			"Total number of cross-tenant operations");
			
		_crossTenantOperationDurationHistogram = _meter.CreateHistogram<int>(
			"cross_tenant_operation_duration_ms",
			"milliseconds",
			"Duration of cross-tenant operations in milliseconds");

		_tenantStats = new ConcurrentDictionary<Guid, TenantMetricsState>();
	}

	public void RecordQueryMetrics(Guid tenantId, string entityType, string queryType, int executionTimeMs, int rowsReturned)
	{
		var tags = new KeyValuePair<string, object?>[]
		{
			new("tenant_id", tenantId.ToString()),
			new("entity_type", entityType),
			new("query_type", queryType)
		};

		// Record OpenTelemetry metrics
		_queryCounter.Add(1, tags);
		_queryDurationHistogram.Record(executionTimeMs, tags);
		_rowsReturnedCounter.Add(rowsReturned, tags);

		// Update in-memory stats
		UpdateTenantStats(tenantId, stats =>
		{
			stats.QueryCount++;
			stats.TotalExecutionTimeMs += executionTimeMs;
			stats.TotalRowsReturned += rowsReturned;
			stats.LastQueryTime = DateTime.UtcNow;
		});
	}

	public void RecordViolation(Guid tenantId, string violationType, string entityType)
	{
		var tags = new KeyValuePair<string, object?>[]
		{
			new("tenant_id", tenantId.ToString()),
			new("violation_type", violationType),
			new("entity_type", entityType)
		};

		// Record OpenTelemetry metrics
		_violationCounter.Add(1, tags);

		// Update in-memory stats
		UpdateTenantStats(tenantId, stats =>
		{
			stats.ViolationCount++;
			stats.LastViolationTime = DateTime.UtcNow;
			
			if (!stats.ViolationsByType.ContainsKey(violationType))
				stats.ViolationsByType[violationType] = 0;
			stats.ViolationsByType[violationType]++;
		});
	}

	public void RecordCrossTenantOperation(string operation, int executionTimeMs)
	{
		var tags = new KeyValuePair<string, object?>[]
		{
			new("operation", operation)
		};

		// Record OpenTelemetry metrics
		_crossTenantOperationCounter.Add(1, tags);
		_crossTenantOperationDurationHistogram.Record(executionTimeMs, tags);

		// Update global cross-tenant stats (not tenant-specific)
		lock (_statsLock)
		{
			foreach (var stats in _tenantStats.Values)
			{
				stats.CrossTenantOperationCount++;
				stats.LastCrossTenantOperationTime = DateTime.UtcNow;
			}
		}
	}

	public Task FlushMetricsAsync()
	{
		return Task.CompletedTask;
	}

	public Task<TenantPerformanceStats> GetTenantStatsAsync(Guid tenantId)
	{
		var stats = _tenantStats.GetOrAdd(tenantId, _ => new TenantMetricsState { TenantId = tenantId });

		lock (stats)
		{
			var result = new TenantPerformanceStats
			{
				TenantId = tenantId,
				QueryCount = stats.QueryCount,
				AverageQueryTimeMs = stats.QueryCount > 0 ? (double)stats.TotalExecutionTimeMs / stats.QueryCount : 0,
				SlowQueryCount = stats.SlowQueryCount,
				ViolationCount = stats.ViolationCount,
				CrossTenantOperationCount = stats.CrossTenantOperationCount,
				PeriodStart = stats.CreatedAt,
				PeriodEnd = DateTime.UtcNow,
				Message = "Stats collected via OpenTelemetry metrics",
				AdditionalMetrics = new Dictionary<string, object>
				{
					["TotalRowsReturned"] = stats.TotalRowsReturned,
					["LastQueryTime"] = stats.LastQueryTime?.ToString("O") ?? "Never",
					["LastViolationTime"] = stats.LastViolationTime?.ToString("O") ?? "Never",
					["LastCrossTenantOperationTime"] = stats.LastCrossTenantOperationTime?.ToString("O") ?? "Never",
					["ViolationsByType"] = stats.ViolationsByType
				}
			};

			return Task.FromResult(result);
		}
	}

	private void UpdateTenantStats(Guid tenantId, Action<TenantMetricsState> updateAction)
	{
		var stats = _tenantStats.GetOrAdd(tenantId, _ => new TenantMetricsState { TenantId = tenantId });
		
		lock (stats)
		{
			updateAction(stats);
		}
	}

	public void Dispose()
	{
		_meter?.Dispose();
	}

	private class TenantMetricsState
	{
		public Guid TenantId { get; set; }
		public int QueryCount { get; set; }
		public long TotalExecutionTimeMs { get; set; }
		public int TotalRowsReturned { get; set; }
		public int SlowQueryCount { get; set; }
		public int ViolationCount { get; set; }
		public int CrossTenantOperationCount { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? LastQueryTime { get; set; }
		public DateTime? LastViolationTime { get; set; }
		public DateTime? LastCrossTenantOperationTime { get; set; }
		public Dictionary<string, int> ViolationsByType { get; set; } = new();
	}
}