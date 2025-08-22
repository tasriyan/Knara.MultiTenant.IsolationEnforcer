using Microsoft.Extensions.DependencyInjection;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;
using Multitenant.Enforcer.PerformanceMonitor;

namespace Multitenant.Enforcer.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static MultitenantIsolationBuilder AddMultiTenantIsolation(this IServiceCollection services,
		Action<MultiTenantOptions>? configure)
	{
		services.AddOptions<MultiTenantOptions>().Configure(opts =>
		{
			if (configure != null)
				configure?.Invoke(opts);
			else
				opts = MultiTenantOptions.DefaultOptions;
		});

		services.AddScoped<ICurrentUserService, CurrentUserService>();
		services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
		services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();
		services.AddScoped<ITenantLookupService, TenantLookupService>();

		//add performance monitoring
		services.AddScoped<ITenantMetricsCollector, LoggingMetricsCollector>();
		services.AddScoped<ITenantPerformanceMonitor, TenantPerformanceMonitor>();


		return new MultitenantIsolationBuilder(services);
	}
}
