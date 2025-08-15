using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Projects;

public interface IProjectRepository 
{
	Task<List<Project>> GetProjectsByManagerAsync(Guid managerId);
	Task<List<Project>> GetActiveProjectsAsync();
	Task<Project?> GetProjectWithTasksAsync(Guid projectId);
}

public class ProjectRepository(TaskMasterDbContext context) : IProjectRepository
{
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

	public async Task<List<Project>> GetActiveProjectsAsync()
	{
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Where(p => p.Status == ProjectStatus.Active)
			.OrderBy(p => p.StartDate)
			.ToListAsync();
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
}
