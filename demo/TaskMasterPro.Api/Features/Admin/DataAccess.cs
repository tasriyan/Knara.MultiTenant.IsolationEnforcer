using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.DataAccess.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Admin;

// THIS SHOULD COMPILE OK BECAUSE NotTenantIsolatedButFineContext does not contain entities that are tenant-isolated (e.g. ITenantIsolated)
public class NotTenantIsolatedAdminDbContext(DbContextOptions<NotTenantIsolatedAdminDbContext> options) : DbContext(options)
{
	public DbSet<Company> Companies { get; set; }
	public DbSet<AdminAuditLog> AuditLogs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());
	}
}
