using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Tasks;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddTasksDataAccess(this IServiceCollection services, IConfiguration config)
	{
		services.AddScoped<TenantRepository<ProjectTask, UnsafeDbContext>>();
		return services;
	}
}

