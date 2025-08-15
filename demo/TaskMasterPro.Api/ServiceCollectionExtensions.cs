using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer;
using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.EntityFramework;

namespace TaskMasterPro.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddDataProvider(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<DefaultDbContext>(options =>
			options.UseSqlite(configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddScoped<ITenantDataProvider, DefaultTenantDataProvider>();

		return services;
	}

	public static IServiceCollection AddCache(this IServiceCollection services)
	{
		// Register in-memory cache for tenant data
		services.AddMemoryCache();
		services.AddScoped<ITenantCache, TenantMemoryCache>();
		services.AddScoped<ITenantCacheManager, TenantCacheManager>();
		return services;
	}
}
