using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Tasks;

//
// DEMO:
//	This code defines multiple implementations for managing Tasks entities in a multi-tenant application
//

public class UnsafeDbContext : DbContext
{
	public UnsafeDbContext(DbContextOptions<DbContext> options) : base(options) { }

	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<ProjectTask> ProjectTasks { get; set; }
	public DbSet<TimeEntry> TimeEntries { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation

		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
		modelBuilder.ApplyConfiguration(new UserConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());

		TaskMasterDbSeed.SeedData(modelBuilder);
	}
}

public class SafeDbContext : TenantDbContext
{
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<ProjectTask> ProjectTasks { get; set; }
	public DbSet<TimeEntry> TimeEntries { get; set; }

	public SafeDbContext(DbContextOptions<SafeDbContext> options,
		ITenantContextAccessor tenantAccessor,
		ILogger<SafeDbContext> logger)
		: base(options, tenantAccessor, logger)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation

		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
		modelBuilder.ApplyConfiguration(new UserConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());

		TaskMasterDbSeed.SeedData(modelBuilder);
	}
}

// DEMO:
// Uses UNSAFE db context
// HOWEVER: SHOULD COMPILE because it derives from TenantRepository THAT ENSURES TENANT ISOLATION
public class TaskTenantRepository(UnsafeDbContext context,
		ITenantContextAccessor tenantAccessor,
		ILogger<TaskTenantRepository> logger)
		: TenantRepository<ProjectTask, UnsafeDbContext>(context, tenantAccessor, logger), ITasksDataAccess
{
	public async Task<ProjectTask?> GetByIdAsync(Guid id)
	{
		return await Query()
			.Include(t => t.AssignedTo)
			.Include(t => t.Project)
			.FirstOrDefaultAsync(t => t.Id == id);
	}

	public async Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId)
	{
		return await Query()
			.AsNoTracking()
			.Include(t => t.AssignedTo)
			.Where(t => t.ProjectId == projectId)
			.OrderBy(t => t.Priority)
			.ThenBy(t => t.DueDate)
			.ToListAsync();
	}

	public async Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId)
	{
		return await Query()
			.AsNoTracking()
			.Include(t => t.Project)
			.Where(t => t.AssignedToId == userId)
			.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
			.ToListAsync();
	}

	public async Task<List<ProjectTask>> GetOverdueTasksAsync()
	{
		var today = DateTime.UtcNow.Date;

		return await Query()
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
		return await Query()
			.AsNoTracking()
			.GroupBy(t => t.Status)
			.ToDictionaryAsync(g => g.Key, g => g.Count());
	}

	public async Task UpdateAsync(ProjectTask task)
	{
		await UpdateAsync(task, cancellationToken: default);
	}
}

// DEMO:
// Using dbcontext directly - not using TenantRepository
// SHOULD COMPILE because SafeDbContext is derived from TenantDbContext THAT ENSURES TENANT ISOLATION
public class TasksDataAccess(SafeDbContext context) : ITasksDataAccess
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

// DEMO:
// Uses UNSAFE db context
// SHOULD NOT COMPILE because it does not derive from TenantRepository or uses a dbcontext derived from TenantDbContext
// ERRORS EXPECTED: Either MTI001 or MTI004
public class UnsafeTasksDataAccess(UnsafeDbContext context) : ITasksDataAccess
{
	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		ProjectTask entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<ProjectTask?> GetByIdAsync(Guid id)
	{
		return await context.ProjectTasks
			.Include(t => t.AssignedTo)
			.Include(t => t.Project)
			.FirstOrDefaultAsync(t => t.Id == id);
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
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

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<List<ProjectTask>> GetTasksByUserAsync(Guid userId)
	{
		return await context.ProjectTasks
			.AsNoTracking()
			.Include(t => t.Project)
			.Where(t => t.AssignedToId == userId)
			.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
			.ToListAsync();
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
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

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<Dictionary<ProjectTaskStatus, int>> GetTaskCountsByStatusAsync()
	{
		return await context.ProjectTasks
			.AsNoTracking()
			.GroupBy(t => t.Status)
			.ToDictionaryAsync(g => g.Key, g => g.Count());
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task UpdateAsync(ProjectTask task)
	{
		context.ProjectTasks.Update(task);
		await context.SaveChangesAsync();
	}
}
