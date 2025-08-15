using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Projects;

public interface IProjectRepository 
{
	Task<Project?> GetByIdAsync(Guid id);
	Task<List<Project>> GetProjectsByManagerAsync(Guid managerId);
	Task<List<Project>> GetActiveProjectsAsync();
	Task<Project?> GetProjectWithTasksAsync(Guid projectId);
	Task AddAsync(Project project);
}

public class ProjectRepository(TaskMasterDbContext context) : IProjectRepository
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects
			.Include(p => p.ProjectManager)
			.FirstOrDefaultAsync(p => p.Id == id);
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

	public async Task AddAsync(Project project)
	{
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}
}
