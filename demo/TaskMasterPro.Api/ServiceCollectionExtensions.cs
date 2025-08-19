using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DependencyInjection;
using TaskMasterPro.Api.Data;

namespace TaskMasterPro.Api;

public static class ServiceCollectionExtensions
{
/*	
	* ------------------------
	* Multi-tenant Enforcer configuration examples
	* ------------------------

	* Simple registration (class with parameterless constructor)
	* services.AddMultiTenantIsolation()
	*	.WithInMemoryTenantCache()
	*	.WithTenantStore<MyTenantStore>();
	*
	* Factory-based registration (for complex dependencies)
	* services.AddMultiTenantIsolation()
	*	.WithTenantDomainCache<RedisTenantCache>(provider => 
	*		new RedisTenantCache(provider.GetRequiredService<IConnectionMultiplexer>()))
	*	.WithTenantStore<EFCoreTenantStore>(provider =>
	*		new EFCoreTenantStore(provider.GetRequiredService<MyDbContext>()));
	*
	* Full configuration example
	* services.AddMultiTenantIsolation(options =>
	*	{
	*		options.CacheTenantResolution = true;
	*		options.CacheExpirationMinutes = 30;
	*	})
	*	.WithInMemoryTenantCache()
	*	.WithTenantStore<EFCoreTenantStore>()
	*	.WithSubdomainResolutionStrategy(options =>
	*	{
	*		options.ExcludedSubdomains = new[] { "www", "api" };
	*	});
*/
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
		services.AddTenantsStore(builder.Configuration);

		services.AddMultiTenantIsolation(options =>
		{
			options.CacheTenantResolution = true;
			options.CacheExpirationMinutes = 30;
		})
			.WithInMemoryTenantCache()
			.WithTenantStore<TenantsStore>()
			.WithSubdomainResolutionStrategy(options =>
			{
				options.CacheMappings = true;
				options.ExcludedSubdomains = ["www", "api", "admin", "localhost", "localhost:5266", "localhost:7058", "localhost:5001"];
				options.SystemAdminClaimValue = "SystemAdmin";
			});

		return services;
	}

	private static IServiceCollection AddTenantsStore(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddDbContext<TenantsStoreDbContext>(options =>
			options.UseSqlite(configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(configuration.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddScoped<ITenantsStore, TenantsStore>();

		return services;
	}
}
