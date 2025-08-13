// MultiTenant.Enforcer.Core/Performance/TenantPerformanceMonitor.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenant.Enforcer.AspNetCore;

namespace MultiTenant.Enforcer.Core
{
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

    /// <summary>
    /// Default implementation of tenant performance monitoring.
    /// </summary>
    public class TenantPerformanceMonitor : ITenantPerformanceMonitor
    {
        private readonly ITenantContextAccessor _tenantAccessor;
        private readonly ILogger<TenantPerformanceMonitor> _logger;
        private readonly PerformanceMonitoringOptions _options;
        private readonly ITenantMetricsCollector? _metricsCollector;

        public TenantPerformanceMonitor(
            ITenantContextAccessor tenantAccessor,
            ILogger<TenantPerformanceMonitor> logger,
            IOptions<MultiTenantOptions> options,
            ITenantMetricsCollector? metricsCollector = null)
        {
            _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value.PerformanceMonitoring;
            _metricsCollector = metricsCollector;
        }

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
                    TenantId = tenantContext.TenantId,
                    IsSystemContext = tenantContext.IsSystemContext,
                    ExecutionTimeMs = executionTimeMs,
                    RowsReturned = rowsReturned,
                    TenantFilterApplied = tenantFilterApplied,
                    Timestamp = DateTime.UtcNow,
                    RequestId = GetCurrentRequestId()
                };

                _logger.LogInformation("TenantQueryPerformance: {@PerformanceLog}", performanceLog);
            }

            // Send to metrics collector if available
            if (_options.CollectMetrics && _metricsCollector != null)
            {
                _metricsCollector.RecordQueryMetrics(tenantContext.TenantId, entityType, queryType, executionTimeMs, rowsReturned);
            }
        }

        public async Task RecordQueryExecutionAsync(string entityType, string queryType, TimeSpan executionTime, int rowsReturned, bool tenantFilterApplied)
        {
            RecordQueryExecution(entityType, queryType, executionTime, rowsReturned, tenantFilterApplied);

            // Async operations like sending to external monitoring systems
            if (_metricsCollector != null)
            {
                await _metricsCollector.FlushMetricsAsync();
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
                TenantId = tenantContext.TenantId,
                Details = details,
                Timestamp = DateTime.UtcNow,
                RequestId = GetCurrentRequestId(),
                UserAgent = GetCurrentUserAgent(),
                IpAddress = GetCurrentIpAddress()
            };

            _logger.LogCritical("TenantViolation: {@ViolationLog}", violationLog);

            // Send to metrics collector for alerting
            if (_metricsCollector != null)
            {
                _metricsCollector.RecordViolation(tenantContext.TenantId, violationType, entityType);
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
                ContextSource = tenantContext.ContextSource,
                IsSystemContext = tenantContext.IsSystemContext,
                Timestamp = DateTime.UtcNow,
                RequestId = GetCurrentRequestId(),
                UserId = GetCurrentUserId()
            };

            _logger.LogWarning("CrossTenantOperation: {@CrossTenantLog}", crossTenantLog);

            if (_metricsCollector != null)
            {
                _metricsCollector.RecordCrossTenantOperation(operation, executionTimeMs);
            }
        }

        public async Task<TenantPerformanceStats> GetStatsAsync()
        {
            if (_metricsCollector == null)
            {
                return new TenantPerformanceStats
                {
                    TenantId = _tenantAccessor.Current.TenantId,
                    Message = "Metrics collection is not enabled"
                };
            }

            return await _metricsCollector.GetTenantStatsAsync(_tenantAccessor.Current.TenantId);
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

    /// <summary>
    /// Interface for collecting tenant metrics for dashboards and alerting.
    /// </summary>
    public interface ITenantMetricsCollector
    {
        /// <summary>
        /// Records query performance metrics.
        /// </summary>
        void RecordQueryMetrics(Guid tenantId, string entityType, string queryType, int executionTimeMs, int rowsReturned);

        /// <summary>
        /// Records a tenant isolation violation.
        /// </summary>
        void RecordViolation(Guid tenantId, string violationType, string entityType);

        /// <summary>
        /// Records a cross-tenant operation.
        /// </summary>
        void RecordCrossTenantOperation(string operation, int executionTimeMs);

        /// <summary>
        /// Flushes pending metrics to storage/monitoring system.
        /// </summary>
        Task FlushMetricsAsync();

        /// <summary>
        /// Gets performance statistics for a tenant.
        /// </summary>
        Task<TenantPerformanceStats> GetTenantStatsAsync(Guid tenantId);
    }

    /// <summary>
    /// Default metrics collector that logs to structured logging.
    /// </summary>
    public class LoggingMetricsCollector : ITenantMetricsCollector
    {
        private readonly ILogger<LoggingMetricsCollector> _logger;

        public LoggingMetricsCollector(ILogger<LoggingMetricsCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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

    /// <summary>
    /// Performance statistics for a tenant.
    /// </summary>
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
}
