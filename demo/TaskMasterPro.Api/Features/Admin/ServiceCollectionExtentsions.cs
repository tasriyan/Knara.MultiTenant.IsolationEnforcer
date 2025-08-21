using Microsoft.EntityFrameworkCore;

namespace TaskMasterPro.Api.Features.Admin;

public static class ServiceCollectionExtentsions
{
	// Note: This is only for demonstration purposes.
	// In a real application, you don't need to register the multiple data contexts
	public static IServiceCollection AddAdminDataContext(this IServiceCollection services, IConfiguration config)
	{
		services.AddDbContext<NotTenantIsolatedAdminDbContext>(options =>
			options.UseSqlite(config.GetConnectionString("DefaultConnection"), sqliteOptions =>
			{
				sqliteOptions.CommandTimeout(config.GetValue<int>("Database:CommandTimeout"));
			}));

		return services;
	}
}
