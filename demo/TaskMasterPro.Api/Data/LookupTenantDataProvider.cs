using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;

namespace Multitenant.Enforcer.EntityFramework;

public class LookupTenantDbContext : DbContext
{
	public LookupTenantDbContext(DbContextOptions<LookupTenantDbContext> options)
		: base(options)
	{
	}
	public DbSet<Company> Companies { get; set; }

	override protected void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());
	}
}

public class LookupTenantDataProvider : ITenantDataProvider
{
	private readonly LookupTenantDbContext _context;
	private readonly ILogger<LookupTenantDataProvider> _logger;

	public LookupTenantDataProvider(
		LookupTenantDbContext context,
		ILogger<LookupTenantDataProvider> logger)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<Guid?> GetActiveTenantIdByDomainAsync(string domain, 
				CancellationToken cancellationToken = default)
	{
		try
		{
			// This assumes you have a Tenants/Companies table with Domain and Id columns
			var query = _context.Companies
				.Where(t => t.Domain == domain && t.IsActive);

			return await query.Select(t => t.Id).FirstOrDefaultAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tenant ID for domain {Domain}", domain);
			return null;
		}
	}

	public async Task<TenantInfo?> GetActiveTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
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
			return Array.Empty<TenantInfo>();
		}
	}
}
