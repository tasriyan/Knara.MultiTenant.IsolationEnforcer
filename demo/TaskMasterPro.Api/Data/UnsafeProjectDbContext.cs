using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Data;

public class UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : DbContext(options)
{
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
