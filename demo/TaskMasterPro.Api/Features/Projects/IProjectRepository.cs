using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Projects
{
	public interface IProjectRepository
	{
		Task AddAsync(Project project);
		Task<Project?> GetByIdAsync(Guid id);
		Task<List<Project>> GetProjectsAsync(string filter = "all");
		Task<List<Project>> GetProjectsByManagerAsync(Guid managerId);
		Task<Project?> GetProjectWithTasksAsync(Guid projectId);
	}
}