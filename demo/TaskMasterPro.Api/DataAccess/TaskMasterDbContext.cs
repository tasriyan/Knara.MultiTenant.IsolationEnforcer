using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.DataAccess.Configurations;
using TaskMasterPro.Api.DataAccess.Data;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess;

public class TaskMasterDbContext : TenantIsolatedDbContext
{
	public DbSet<Company> Companies { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<ProjectTask> ProjectTasks { get; set; }
	public DbSet<TimeEntry> TimeEntries { get; set; }
	public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

	public TaskMasterDbContext(DbContextOptions<TaskMasterDbContext> options,
		ITenantContextAccessor tenantAccessor,
		ILogger<TaskMasterDbContext> logger)
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
