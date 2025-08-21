using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Projects;

public interface IProjectRepository
{
	Task AddAsync(Project project);
	Task<Project?> GetByIdAsync(Guid id);
	Task<List<Project>> GetProjectsAsync(string filter = "all");
	Task<List<Project>> GetProjectsByManagerAsync(Guid managerId);
	Task<Project?> GetProjectWithTasksAsync(Guid projectId);
}

// Uses UNSAFE db context
// HOWEVER: SHOULD COMPILE because it derives from TenantRepository THAT ENSURES TENANT ISOLATION
public class TenantIsolatedProjectRepository(UnsafeDbContext context, 
		ITenantContextAccessor tenantAccessor, 
		ILogger<TenantIsolatedProjectRepository> logger)
		: TenantRepository<Project, UnsafeDbContext>(context, tenantAccessor, logger), IProjectRepository
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await GetByIdAsync(id, cancellationToken: default);
	}

	public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
	{
		// Global query filter automatically applies tenant isolation
		return await Query()
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Where(p => p.ProjectManagerId == managerId)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
	{
		return filter switch
		{
			"active" => await Query()
									.AsNoTracking()
									.Include(p => p.ProjectManager)
									.Where(p => p.Status == ProjectStatus.Active)
									.OrderBy(p => p.StartDate)
									.ToListAsync(),
			_ => await Query()
										.AsNoTracking()
										.Include(p => p.ProjectManager)
										.OrderBy(p => p.StartDate)
										.ToListAsync(),
		};
	}

	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
	{
		return await Query()
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Include(p => p.Tasks)
			.ThenInclude(t => t.AssignedTo)
			.FirstOrDefaultAsync(p => p.Id == projectId);
	}

	public async Task AddAsync(Project project)
	{
		await AddAsync(project);
	}
}

// SHOULD COMPILE because TenantIsolatedProjectsDbContext is derived from TenantDbContext THAT ENSURES TENANT ISOLATION
public class TenantIsolatedProjectRepositorySecondOption(TaskMasterDbContext context) : IProjectRepository
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
	{
		// Global query filter automatically applies tenant isolation
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Where(p => p.ProjectManagerId == managerId)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
	{
		return filter switch
		{
			"active" => await context.Projects
								.AsNoTracking()
								.Include(p => p.ProjectManager)
								.Where(p => p.Status == ProjectStatus.Active)
								.OrderBy(p => p.StartDate)
								.ToListAsync(),
			_ => await context.Projects
										.AsNoTracking()
										.Include(p => p.ProjectManager)
										.OrderBy(p => p.StartDate)
										.ToListAsync(),
		};
	}

	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
	{
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Include(p => p.Tasks)
			.ThenInclude(t => t.AssignedTo)
			.FirstOrDefaultAsync(p => p.Id == projectId);
	}

	public async Task AddAsync(Project project)
	{
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}
}

#region UnsafeProjectRepository: UNCOMMENT CODE TO EXPERIENCE ERRORS (MTI001 or MTI004)
// Uses UNSAFE db context
// SHOULD NOT COMPILE because it does not derive from TenantRepository or uses a dbcontext derived from TenantDbContext
// ERRORS EXPECTED: Either MTI001 or MTI004

//public class UnsafeProjectRepository(UnsafeDbContext context) : IProjectRepository
//{
//	// ERRORS EXPECTED: Either MTI001 or MTI004
//	// BECAUSE:
//	//		Project entity is derived from ITenantIsolated
//	//		and UnsafeDbContext is not derived from TenantDbContext
//	public async Task<Project?> GetByIdAsync(Guid id)
//	{
//		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
//	}

//	// ERRORS EXPECTED: Either MTI001 or MTI004
//	// BECAUSE:
//	//		Project entity is derived from ITenantIsolated
//	//		and UnsafeDbContext is not derived from TenantDbContext
//	public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
//	{
//		// Global query filter automatically applies tenant isolation
//		return await context.Projects
//			.AsNoTracking()
//			.Include(p => p.ProjectManager)
//			.Where(p => p.ProjectManagerId == managerId)
//			.OrderBy(p => p.Name)
//			.ToListAsync();
//	}

//	// ERRORS EXPECTED: Either MTI001 or MTI004
//	// BECAUSE:
//	//		Project entity is derived from ITenantIsolated
//	//		and UnsafeDbContext is not derived from TenantDbContext
//	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
//	{
//		return filter switch
//		{
//			"active" => await context.Projects
//								.AsNoTracking()
//								.Include(p => p.ProjectManager)
//								.Where(p => p.Status == ProjectStatus.Active)
//								.OrderBy(p => p.StartDate)
//								.ToListAsync(),
//			_ => await context.Projects
//										.AsNoTracking()
//										.Include(p => p.ProjectManager)
//										.OrderBy(p => p.StartDate)
//										.ToListAsync(),
//		};
//	}

//	// ERRORS EXPECTED: Either MTI001 or MTI004
//	// BECAUSE:
//	//		Project entity is derived from ITenantIsolated
//	//		and UnsafeDbContext is not derived from TenantDbContext
//	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
//	{
//		return await context.Projects
//			.AsNoTracking()
//			.Include(p => p.ProjectManager)
//			.Include(p => p.Tasks)
//			.ThenInclude(t => t.AssignedTo)
//			.FirstOrDefaultAsync(p => p.Id == projectId);
//	}

//	// ERRORS EXPECTED: Either MTI001 or MTI004
//	// BECAUSE:
//	//		Project entity is derived from ITenantIsolated
//	//		and UnsafeDbContext is not derived from TenantDbContext
//	public async Task AddAsync(Project project)
//	{
//		context.Projects.Add(project);
//		await context.SaveChangesAsync();
//	}
//}
#endregion

