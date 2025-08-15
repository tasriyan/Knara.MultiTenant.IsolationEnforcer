namespace Multitenant.Enforcer.PerformanceMonitor;

/// <summary>
/// Interface for collecting tenant metrics for dashboards and alerting.
/// </summary>
public interface ITenantMetricsCollector
    {
        void RecordQueryMetrics(Guid tenantId, string entityType, string queryType, int executionTimeMs, int rowsReturned);

        /// Records a tenant isolation violation.
        void RecordViolation(Guid tenantId, string violationType, string entityType);

        /// Records a cross-tenant operation.
        void RecordCrossTenantOperation(string operation, int executionTimeMs);

        /// Flushes pending metrics to storage/monitoring system.
        Task FlushMetricsAsync();

        /// Gets performance statistics for a tenant.
        Task<TenantPerformanceStats> GetTenantStatsAsync(Guid tenantId);
    }
