using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.CodeAnalysisDemo;

// File contains classes that were intentionally designed to not compile to test Roslyn analyzer behavior
// Uncomment to experience the compilation errors and warnings from the custom Roslyn analyzers
// ERRORS EXPECTED:
//		MTI001 - Direct DbSet access - Compilation error
//		MTI003 - Potential filter bypasses - Warning
//		MTI004 - Entities without repositories - Compilation error

public class ShouldNotCompileRepository(ShouldNotCompileDBContext context)
{
	public async Task AddAsync(Project project)
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}

	public async Task<Project?> GetByIdAsync(Guid id)
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	public async Task<List<Project>> GetAllAsync()
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		return await context.Projects.ToListAsync();
	}

	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		switch (filter)
		{
			case "active":
				return await context.Projects
						.AsNoTracking()
						.Include(p => p.ProjectManager)
						.Where(p => p.Status == ProjectStatus.Active)
						.OrderBy(p => p.StartDate)
						.ToListAsync();
			default:
				return await context.Projects
							.AsNoTracking()
							.Include(p => p.ProjectManager)
							.OrderBy(p => p.StartDate)
							.ToListAsync();
		}
	}

	public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Where(p => p.ProjectManagerId == managerId)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
	{
		// This method is intentionally designed to not compile to test Roslyn analyzer behavior
		// ERRORS EXPECTED: Either MTI001 or MTI004
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Include(p => p.Tasks)
			.ThenInclude(t => t.AssignedTo)
			.FirstOrDefaultAsync(p => p.Id == projectId);
	}
}
