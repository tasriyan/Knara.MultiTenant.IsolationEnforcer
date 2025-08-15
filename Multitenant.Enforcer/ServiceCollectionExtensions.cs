using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using Multitenant.Enforcer.PerformanceMonitor;
using Multitenant.Enforcer.Resolvers;

namespace Multitenant.Enforcer;


public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMultiTenantIsolation<TDbContext>(
		this IServiceCollection services,
		Action<MultiTenantOptions>? configure = null)
		where TDbContext : TenantDbContext
	{
		var options = new MultiTenantOptions();
		configure?.Invoke(options);

		services.AddSingleton(options);

		// Register core services
		services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
		services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();

		// Register tenant resolver based on configuration
		RegisterTenantResolver(services, options);

		// Register performance monitoring if enabled
		if (options.PerformanceMonitoring.Enabled)
		{
			services.AddScoped<ITenantPerformanceMonitor, TenantPerformanceMonitor>();
		}

		// Register tenant lookup service if using subdomain resolution
		if (options.DefaultTenantResolver == typeof(SubdomainTenantResolver))
		{
			services.TryAddScoped<ITenantLookupService, TenantLookupService>();
		}

		return services;
	}

	private static void RegisterTenantResolver(IServiceCollection services, MultiTenantOptions options)
	{
		if (options.DefaultTenantResolver == typeof(SubdomainTenantResolver))
		{
			services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
		}
		else if (options.DefaultTenantResolver == typeof(JwtTenantResolver))
		{
			services.AddScoped<ITenantResolver, JwtTenantResolver>();
		}
		else if (options.CustomTenantResolvers.Any())
		{
			// Register composite resolver with custom resolvers
			foreach (var resolverType in options.CustomTenantResolvers)
			{
				services.AddScoped(typeof(ITenantResolver), resolverType);
			}

			services.AddScoped<ITenantResolver>(provider =>
			{
				var resolvers = provider.GetServices<ITenantResolver>()
					.Where(r => r.GetType() != typeof(CompositeTenantResolver))
					.ToArray();

				var logger = provider.GetRequiredService<ILogger<CompositeTenantResolver>>();
				return new CompositeTenantResolver(resolvers, logger);
			});
		}
		else
		{
			// Default to JWT resolver
			services.AddScoped<ITenantResolver, JwtTenantResolver>();
		}
	}
}
