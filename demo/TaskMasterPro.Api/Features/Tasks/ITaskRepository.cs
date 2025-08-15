using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Tasks;

public interface ITaskRepository
{
	Task<ProjectTask?> GetByIdAsync(Guid id);
	Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId);
	Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId);
	Task<List<ProjectTask>> GetOverdueTasksAsync();
	Task<Dictionary<ProjectTaskStatus, int>> GetTaskCountsByStatusAsync();
	Task UpdateAsync(ProjectTask task);
}

public class TaskRepository(TaskMasterDbContext context) : ITaskRepository
{
	public async Task<ProjectTask?> GetByIdAsync(Guid id)
	{
		return await context.ProjectTasks
			.Include(t => t.AssignedTo)
			.Include(t => t.Project)
			.FirstOrDefaultAsync(t => t.Id == id);
	}

	public async Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId)
	{
		return await context.ProjectTasks
			.AsNoTracking()
			.Include(t => t.AssignedTo)
			.Where(t => t.ProjectId == projectId)
			.OrderBy(t => t.Priority)
			.ThenBy(t => t.DueDate)
			.ToListAsync();
	}

	public async Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId)
	{
		return await context.ProjectTasks
			.AsNoTracking()
			.Include(t => t.Project)
			.Where(t => t.AssignedToId == userId)
			.Where(t => t.Status != ProjectTaskStatus.Done)
			.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
			.ToListAsync();
	}

	public async Task<List<ProjectTask>> GetOverdueTasksAsync()
	{
		var today = DateTime.UtcNow.Date;

		return await context.ProjectTasks
			.AsNoTracking()
			.Include(t => t.AssignedTo)
			.Include(t => t.Project)
			.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date < today)
			.Where(t => t.Status != ProjectTaskStatus.Done)
			.OrderBy(t => t.DueDate)
			.ToListAsync();
	}

	public async Task<Dictionary<ProjectTaskStatus, int>> GetTaskCountsByStatusAsync()
	{
		return await context.ProjectTasks
			.AsNoTracking()
			.GroupBy(t => t.Status)
			.ToDictionaryAsync(g => g.Key, g => g.Count());
	}

	public async Task UpdateAsync(ProjectTask task)
	{
		context.ProjectTasks.Update(task);
		await context.SaveChangesAsync();
	}
}
