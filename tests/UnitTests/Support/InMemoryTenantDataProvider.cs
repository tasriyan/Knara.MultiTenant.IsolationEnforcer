using Knara.MultiTenant.IsolationEnforcer.Core;
using Multitenant.Enforcer;

namespace UnitTests.Support;

/// <summary>
/// In-memory implementation for testing or simple scenarios.
/// </summary>
public class InMemoryTenantDataProvider(TenantInfo[] tenants) : ITenantStore
{
	private readonly TenantInfo[] _tenants = tenants ?? [];

	public Task<TenantInfo?> GetTenantInfoByDomainAsync(string domain, CancellationToken cancellationToken = default)
	{
		var tenant = _tenants.FirstOrDefault(t =>
			t.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && t.IsActive);

		return Task.FromResult(tenant);
	}

	public Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken)
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
