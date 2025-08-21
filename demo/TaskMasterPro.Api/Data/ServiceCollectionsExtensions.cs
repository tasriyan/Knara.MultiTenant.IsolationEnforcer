using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;

namespace TaskMasterPro.Api.Data;

public static class ServiceCollectionsExtensions
{
	// For demo purposes, we are using the same Sqlite database for all three DbContexts.
	// In a real application, you would likely use different databases or connection strings.
	// And you might not need have and to register all three DbContexts.
	public static IServiceCollection AddGlobalDataAccess(this IServiceCollection services, IConfiguration config)
	{
		services.AddDbContext<TaskMasterDbContext>(options =>
			options.UseSqlite(config.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(config.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddDbContext<TenantsStoreDbContext>(options =>
			options.UseSqlite(config.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(config.GetValue<int>("Database:CommandTimeout"));
			}));

		services.AddDbContext<UnsafeDbContext>(options =>
				options.UseSqlite(config.GetConnectionString("DefaultConnection"), sqliteOptions =>
				{
					sqliteOptions.CommandTimeout(config.GetValue<int>("Database:CommandTimeout"));
				}));

		services.AddScoped<IReadOnlyTenants, TaskMasterProTenants>();
		return services;
	}
}
