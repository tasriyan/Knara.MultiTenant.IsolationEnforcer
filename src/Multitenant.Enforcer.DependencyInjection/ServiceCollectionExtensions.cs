using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

		services.TryAddScoped<ICurrentUserService, CurrentUserService>();
		services.TryAddScoped<ITenantLookupService, TenantLookupService>();
		services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
		services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();

		//add performance monitoring
		services.TryAddScoped<ITenantMetricsCollector, LoggingMetricsCollector>();
		services.TryAddScoped<ITenantPerformanceMonitor, TenantPerformanceMonitor>();


		return new MultitenantIsolationBuilder(services);
	}
}
