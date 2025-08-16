using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer;
using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.EntityFramework;
using Multitenant.Enforcer.Resolvers;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMultiTenantEnforcer(this WebApplicationBuilder builder)
	{
		// Database configuration - SQLite In-Memory
		builder.Services.AddDbContext<TaskMasterDbContext>(options =>
			options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		// Multi-tenant dependencies
		builder.Services.AddDataProvider(builder.Configuration);
		builder.Services.AddCache();

		// Multi-tenant isolation enforcer
		builder.Services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
		{
			options.DefaultTenantResolver = typeof(SubdomainTenantResolver);
			options.PerformanceMonitoring = new Multitenant.Enforcer.PerformanceMonitor.PerformanceMonitoringOptions
			{
				Enabled = true,
			};
			options.LogViolations = true;
		});
		return builder.Services;
	}

	private static IServiceCollection AddDataProvider(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<DefaultDbContext>(options =>
			options.UseSqlite(configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddScoped<ITenantDataProvider, DefaultTenantDataProvider>();

		return services;
	}

	private static IServiceCollection AddCache(this IServiceCollection services)
	{
		// Register in-memory cache for tenant data
		services.AddMemoryCache();
		services.AddScoped<ITenantCache, TenantMemoryCache>();
		services.AddScoped<ITenantCacheManager, TenantCacheManager>();
		return services;
	}
}
