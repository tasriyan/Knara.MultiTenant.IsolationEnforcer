using Knara.MultiTenant.IsolationEnforcer.AspNetCore;
using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.PerformanceMonitor;
using Knara.MultiTenant.IsolationEnforcer.TenantResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knara.MultiTenant.IsolationEnforcer.Extensions;

public static class ServiceCollectionExtensions
{
	public static MultitenantIsolationBuilder AddMultiTenantIsolation(this IServiceCollection services,
		Action<MultiTenantOptions>? configure = null)
	{
		services.AddOptions<MultiTenantOptions>().Configure(opts =>
		{
			if (configure != null)
				configure?.Invoke(opts);
			else
				opts = MultiTenantOptions.DefaultOptions;
		});

		// Core services - always required
		services.TryAddScoped<ITenantLookupService, TenantLookupService>();
		services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
		services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();
		
		// Performance monitoring - MANDATORY (opinionated library philosophy)
		// But allow configuration through PerformanceMonitoringOptions
		services.AddOptions<PerformanceMonitoringOptions>().Configure(opts =>
		{
			// Default configuration - can be overridden via WithPerformanceMonitoring()
			opts.Enabled = true;
			opts.SlowQueryThresholdMs = 1000;
			opts.CollectMetrics = true;
		});
		
		services.TryAddScoped<ICurrentUserService, CurrentUserService>();
		// add default implementation of ITenantMetricsCollector but allow override with any custom ITenantMetricsCollector implementation
		services.TryAddScoped<ITenantMetricsCollector, LoggingMetricsCollector>();
		services.TryAddScoped<ITenantPerformanceMonitor, TenantPerformanceMonitor>();

		return new MultitenantIsolationBuilder(services);
	}
}

