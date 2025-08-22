using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multitenant.Enforcer.Core;
using Serilog;

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

		return services;
	}

	// Ensure the TaskMasterPro database is created
	public static WebApplication EnsureDatabaseCreated(this WebApplication app)
	{
		try
		{
			Log.Information("Ensuring tasks master database is created");

			using var scope = app.Services.CreateScope();
			var scopedServices = scope.ServiceProvider;
			var context = scopedServices.GetRequiredService<TaskMasterDbContext>();
			context.Database.EnsureCreated();

			Log.Information("Tasks master database creation check completed");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "An error occurred while ensuring the database was created");
			throw;
		}
		return app;
	}
}
