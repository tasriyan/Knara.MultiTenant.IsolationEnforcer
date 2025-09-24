using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.DataAccess;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Tasks;

// DEMO:
// Uses UNSAFE db context
// SHOULD NOT COMPILE because it does not derive from TenantRepository or uses a dbcontext derived from TenantDbContext
// ERRORS EXPECTED: Either MTI001 or MTI004

//UNCOMMENT TO EXPERIENCE THE ERRORS
// public class UnsafeTasksDataAccess(UnsafeDbContext context)
// {
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		ProjectTask entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task<ProjectTask?> GetByIdAsync(Guid id)
// 	{
// 		return await context.ProjectTasks
// 			.Include(t => t.AssignedTo)
// 			.Include(t => t.Project)
// 			.FirstOrDefaultAsync(t => t.Id == id);
// 	}
//
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		Project entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId)
// 	{
// 		return await context.ProjectTasks
// 			.AsNoTracking()
// 			.Include(t => t.AssignedTo)
// 			.Where(t => t.ProjectId == projectId)
// 			.OrderBy(t => t.Priority)
// 			.ThenBy(t => t.DueDate)
// 			.ToListAsync();
// 	}
//
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		Project entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId)
// 	{
// 		return await context.ProjectTasks
// 			.AsNoTracking()
// 			.Include(t => t.Project)
// 			.Where(t => t.AssignedToId == userId)
// 			.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
// 			.ToListAsync();
// 	}
//
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		Project entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task<List<ProjectTask>> GetOverdueTasksAsync()
// 	{
// 		var today = DateTime.UtcNow.Date;
//
// 		return await context.ProjectTasks
// 			.AsNoTracking()
// 			.Include(t => t.AssignedTo)
// 			.Include(t => t.Project)
// 			.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date < today)
// 			.Where(t => t.Status != ProjectTaskStatus.Done)
// 			.OrderBy(t => t.DueDate)
// 			.ToListAsync();
// 	}
//
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		Project entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task<Dictionary<ProjectTaskStatus, int>> GetTaskCountsByStatusAsync()
// 	{
// 		return await context.ProjectTasks
// 			.AsNoTracking()
// 			.GroupBy(t => t.Status)
// 			.ToDictionaryAsync(g => g.Key, g => g.Count());
// 	}
//
// 	// ERRORS EXPECTED: Either MTI001 or MTI004
// 	// BECAUSE:
// 	//		Project entity is derived from ITenantIsolated
// 	//		and UnsafeDbContext is not derived from TenantDbContext
// 	public async Task UpdateAsync(ProjectTask task)
// 	{
// 		context.ProjectTasks.Update(task);
// 		await context.SaveChangesAsync();
// 	}
// }


