using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer;
using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;

namespace TaskMasterPro.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMultiTenantEnforcer(this WebApplicationBuilder builder)
	{
		var services = builder.Services;
		// Database configuration - SQLite In-Memory
		services.AddDbContext<TaskMasterDbContext>(options =>
			options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		// Multi-tenant dependencies
		services.AddLoookupTenantDataProvider(builder.Configuration);
		services.AddLookupTenantCache();

		// Basic multi-tenant isolation enforcer with default JwtTenantResolverOptions
		//builder.Services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
		//{
		//	options.DefaultTenantResolver = typeof(JwtTenantResolver);
		//});

		// Advanced multi-tenant isolation enforcer
		services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
		{
			options.UseSubdomainTenantResolver(config =>
			{
				config.CacheMappings = true;
				config.ExcludedSubdomains = ["www", "api", "admin", "localhost", "localhost:5266", "localhost:7058", "localhost:5001"];
				config.SystemAdminClaimValue = "SystemAdmin";			
			});
			options.PerformanceMonitoring = new Multitenant.Enforcer.PerformanceMonitor.PerformanceMonitoringOptions
			{
				Enabled = true,
			};
			options.LogViolations = true;
		});
		return services;
	}

	private static IServiceCollection AddLoookupTenantDataProvider(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<LookupTenantDbContext>(options =>
			options.UseSqlite(configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddScoped<ITenantDataProvider, LookupTenantDataProvider>();

		return services;
	}

	private static IServiceCollection AddLookupTenantCache(this IServiceCollection services)
	{
		// Register in-memory cache for tenant data
		services.AddMemoryCache();
		services.AddScoped<ITenantCache, TenantMemoryCache>();
		services.AddScoped<ITenantCacheManager, TenantCacheManager>();
		return services;
	}
}
