using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Data;

// File contains classes that were intentionally designed to not compile to test Roslyn analyzer behavior
// Uncomment to experience the compilation errors and warnings from the custom Roslyn analyzers
// ERRORS EXPECTED:
//		MTI001 - Direct DbSet access - Compilation error
//		MTI003 - Potential filter bypasses - Warning
//		MTI004 - Entities without repositories - Compilation error


// This class should not compile because
//	Because
//		it is directly derived from DbContext instead of TenantDbContext while using properties implementic ITenantIsolated
//  ERRORS EXPECTED: MTI006
public class ShouldNotCompileDBContext : DbContext
{
	public ShouldNotCompileDBContext(DbContextOptions<DbContext> options) : base(options) { }

	public DbSet<Company> Companies { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<ProjectTask> ProjectTasks { get; set; }
	public DbSet<TimeEntry> TimeEntries { get; set; }
	public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

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
