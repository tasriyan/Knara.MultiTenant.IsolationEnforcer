using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.EntityFramework;

/// <summary>
/// Provides tenant data retrieval functionality using Entity Framework.
/// </summary>
/// <remarks>Generates unfiltered queries.</remarks>
public class DefaultTenantDataProvider : ITenantDataProvider
{
	private readonly DbContext _context;
	private readonly ILogger<DefaultTenantDataProvider> _logger;

	public DefaultTenantDataProvider(
		DbContext context,
		ILogger<DefaultTenantDataProvider> logger)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	// This assumes you have a Tenants/Companies table with Domain and Id columns
	public async Task<Guid?> GetActiveTenantIdByDomainAsync(string domain, CancellationToken cancellationToken)
	{
		try
		{
			var query = _context.Set<TenantEntity>()
				.Where(t => t.Domain == domain && t.IsActive)
				.Select(t => t.Id);

			return await query.FirstOrDefaultAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving tenant ID for domain {Domain}", domain);
			return null;
		}
	}

	// This overload allows for additional filtering via a predicate
	public async Task<Guid?> GetActiveTenantIdByDomainAsync(string domain, 
				System.Linq.Expressions.Expression<Func<TenantEntity, bool>> predicate,  
				CancellationToken cancellationToken)
	{
		try
		{
			var query = _context.Set<TenantEntity>()
				.Where(t => t.Domain == domain && t.IsActive)
				.Where(predicate)
				.Select(t => t.Id);

			return await query.FirstOrDefaultAsync(cancellationToken);
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
			return await _context.Set<TenantEntity>()
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
			return await _context.Set<TenantEntity>()
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
