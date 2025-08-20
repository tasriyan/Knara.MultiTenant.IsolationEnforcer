using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Projects;

//
// DEMO:
//	This code defines multiple implementations for managing Project entities in a multi-tenant application
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

		modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
		modelBuilder.ApplyConfiguration(new UserConfiguration());

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

		modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectTaskConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
		modelBuilder.ApplyConfiguration(new UserConfiguration());

		TaskMasterDbSeed.SeedData(modelBuilder);
	}
}

// DEMO:
// Uses UNSAFE db context
// HOWEVER: SHOULD COMPILE because it derives from TenantRepository THAT ENSURES TENANT ISOLATION
public class ProjectTenantRepository(UnsafeDbContext context, 
		ITenantContextAccessor tenantAccessor, 
		ILogger<ProjectTenantRepository> logger)
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
		switch (filter)
		{
			case "active":
					return await Query()
						.AsNoTracking()
						.Include(p => p.ProjectManager)
						.Where(p => p.Status == ProjectStatus.Active)
						.OrderBy(p => p.StartDate)
						.ToListAsync();
			default:
				return await Query()
							.AsNoTracking()
							.Include(p => p.ProjectManager)
							.OrderBy(p => p.StartDate)
							.ToListAsync();
		}
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

// DEMO:
// Using dbcontext directly - not using TenantRepository
// SHOULD COMPILE because SafeDbContext is derived from TenantDbContext THAT ENSURES TENANT ISOLATION
public class ProjectRepository(SafeDbContext context) : IProjectRepository
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
				return await Query()
							.AsNoTracking()
							.Include(p => p.ProjectManager)
							.OrderBy(p => p.StartDate)
							.ToListAsync();
		}
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

// DEMO:
// Uses UNSAFE db context
// SHOULD NOT COMPILE because it does not derive from TenantRepository or uses a dbcontext derived from TenantDbContext
// ERRORS EXPECTED: Either MTI001 or MTI004
public class UnsafeProjectRepository(UnsafeDbContext context) : IProjectRepository
{
	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
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

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
	{
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
				return await Query()
							.AsNoTracking()
							.Include(p => p.ProjectManager)
							.OrderBy(p => p.StartDate)
							.ToListAsync();
		}
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
	{
		return await context.Projects
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Include(p => p.Tasks)
			.ThenInclude(t => t.AssignedTo)
			.FirstOrDefaultAsync(p => p.Id == projectId);
	}

	// ERRORS EXPECTED: Either MTI001 or MTI004
	// BECAUSE:
	//		Project entity is derived from ITenantIsolated
	//		and UnsafeDbContext is not derived from TenantDbContext
	public async Task AddAsync(Project project)
	{
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}
}
