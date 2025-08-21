using Microsoft.EntityFrameworkCore;

namespace TaskMasterPro.Api.Features.Projects;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddProjectsDataAccess(this IServiceCollection services, IConfiguration config)
	{
		// Register the repository using the tenant-isolated DbContext
		services.AddScoped<IProjectRepository, TenantIsolatedProjectRepositorySecondOption>();

		// Register the repository using the unsafe DbContext
		// services.AddScoped<IProjectRepository, TenantIsolatedProjectRepository>();
		return services;
	}
}