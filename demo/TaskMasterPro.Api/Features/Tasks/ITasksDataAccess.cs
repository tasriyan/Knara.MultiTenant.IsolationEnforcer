using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Tasks;

public interface ITasksDataAccess
{
	Task<ProjectTask?> GetByIdAsync(Guid id);
	Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId);
	Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId);
	Task<List<ProjectTask>> GetOverdueTasksAsync();
	Task<Dictionary<ProjectTaskStatus, int>> GetTaskCountsByStatusAsync();
	Task UpdateAsync(ProjectTask task);
}
