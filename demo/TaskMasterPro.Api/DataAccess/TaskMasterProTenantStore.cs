using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.DataAccess.Configurations;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess;

public class TenantStoreDbContext(DbContextOptions<TenantStoreDbContext> options) : DbContext(options)
{
	public DbSet<Company> Companies { get; set; }

	override protected void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());
	}
}

public class TaskMasterProTenantStore(
	TenantStoreDbContext context,
	ILogger<TaskMasterProTenantStore> logger) : ITenantStore
{
	private readonly TenantStoreDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
	private readonly ILogger<TaskMasterProTenantStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task<TenantInfo?> GetTenantInfoByDomainAsync(string domain,
				CancellationToken cancellationToken = default)
	{
		try
		{
			return await _context.Companies
				.Where(t => t.Domain == domain && t.IsActive).Select(t => new TenantInfo
				{
					Id = t.Id,
					Name = t.Name,
					Domain = t.Domain,
					IsActive = t.IsActive,
					CreatedAt = t.CreatedAt
				})
				.FirstOrDefaultAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tenant ID for domain {Domain}", domain);
			return null;
		}
	}

	public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		try
		{
			return await _context.Companies
				.Where(t => t.Id == tenantId && t.IsActive)
				.Select(t => new TenantInfo
				{
					Id = t.Id,
					Name = t.Name,
					Domain = t.Domain,
					IsActive = t.IsActive,
					CreatedAt = t.CreatedAt
				})
				.FirstOrDefaultAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tenant info for {TenantId}", tenantId);
			return null;
		}
	}

	public async Task<TenantInfo[]> GetAllActiveTenantsAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await _context.Companies
				.Where(t => t.IsActive)
				.Select(t => new TenantInfo
				{
					Id = t.Id,
					Name = t.Name,
					Domain = t.Domain,
					IsActive = t.IsActive,
					CreatedAt = t.CreatedAt
				})
				.ToArrayAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving all active tenants");
			return [];
		}
	}

}
