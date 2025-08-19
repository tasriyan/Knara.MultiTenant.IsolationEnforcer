namespace Multitenant.Enforcer.PerformanceMonitor;

public class PerformanceMonitoringOptions
{
	/// Whether performance monitoring is enabled.
	public bool Enabled { get; set; } = true;

	/// Threshold in milliseconds for logging slow queries.
	public int SlowQueryThresholdMs { get; set; } = 1000;

	/// Whether to log query execution plans.
	public bool LogQueryPlans { get; set; } = false;

	/// Whether to collect metrics for performance dashboards.
	public bool CollectMetrics { get; set; } = true;
}
