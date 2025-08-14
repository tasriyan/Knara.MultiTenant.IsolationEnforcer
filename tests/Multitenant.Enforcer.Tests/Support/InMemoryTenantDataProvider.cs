using Multitenant.Enforcer;
using Multitenant.Enforcer.Core;
using System.Linq.Expressions;

namespace MultiTenant.Enforcer.Tests.Support;

/// <summary>
/// In-memory implementation for testing or simple scenarios.
/// </summary>
public class InMemoryTenantDataProvider(TenantInfo[] tenants) : ITenantDataProvider
{
	private readonly TenantInfo[] _tenants = tenants ?? [];

	public Task<Guid?> GetActiveTenantIdByDomainAsync(string domain,
		Expression<Func<TenantEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
	{
		var tenant = _tenants.FirstOrDefault(t =>
			t.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && t.IsActive);

		return Task.FromResult(tenant?.Id);
	}

	public Task<TenantInfo?> GetActiveTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId && t.IsActive);
		return Task.FromResult(tenant);
	}

	public Task<TenantInfo[]> GetAllActiveTenantsAsync(CancellationToken cancellationToken)
	{
		var activeTenants = _tenants.Where(t => t.IsActive).ToArray();
		return Task.FromResult(activeTenants);
	}
}
