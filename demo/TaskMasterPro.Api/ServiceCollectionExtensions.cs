using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DependencyInjection;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.DataAccess;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Features.Admin;
using TaskMasterPro.Api.Features.Projects;
using TaskMasterPro.Api.Features.Tasks;

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

		services.AddMultiTenantIsolation(options =>
			{
				options.CacheTenantResolution = true;
				options.CacheExpirationMinutes = 30;
			})
			.WithInMemoryTenantCache()
			.WithTenantsStore<TaskMasterProTenantStore>()
			.WithSubdomainResolutionStrategy(options =>
			{
				options.CacheMappings = true;
				options.ExcludedSubdomains = ["www", "api", "admin", "localhost", "localhost:5266", "localhost:7058", "localhost:5001"];
				options.SystemAdminClaimValue = "SystemAdmin";
			});

		return services;
	}

	public static IServiceCollection AddTaskMasterProServices(this IServiceCollection services, IConfiguration config)
	{
		// Admin requeired services
		services.AddDbContext<NotTenantIsolatedAdminDbContext>(options =>
			options.UseSqlite(config.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(config.GetValue<int>("Database:CommandTimeout"));
			}));

		// Projects required services
		services.AddScoped<IProjectRepository, TenantIsolatedProjectRepositorySecondOption>();

		// Register the repository using the unsafe DbContext
		// services.AddScoped<IProjectRepository, TenantIsolatedProjectRepository>();

		// Tasks required services
		services.AddScoped<TenantIsolatedRepository<ProjectTask, UnsafeDbContext>>();

		return services;
	}

	public static IServiceCollection ConfigureEntityFramework(this WebApplicationBuilder builder)
	{
		var services = builder.Services;
		var configuration = builder.Configuration;

		// Database configuration - SQLite In-Memory
		services.AddGlobalDataAccess(configuration);

		return services;
	}
}
