namespace Multitenant.Enforcer.PerformanceMonitor;

public class TenantPerformanceStats
{
	public Guid TenantId { get; set; }
	public int QueryCount { get; set; }
	public double AverageQueryTimeMs { get; set; }
	public int SlowQueryCount { get; set; }
	public int ViolationCount { get; set; }
	public int CrossTenantOperationCount { get; set; }
	public DateTime PeriodStart { get; set; } = DateTime.UtcNow.AddHours(-1);
	public DateTime PeriodEnd { get; set; } = DateTime.UtcNow;
	public string? Message { get; set; }
	public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}
