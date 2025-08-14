using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.Core;

namespace MultiTenant.Enforcer.Tests.Support;

/// <summary>
/// In-memory implementation for testing or simple scenarios.
/// </summary>
public class InMemoryTenantDataProvider : ITenantDataProvider
{
	private readonly TenantInfo[] _tenants;

	public InMemoryTenantDataProvider(TenantInfo[] tenants)
	{
		_tenants = tenants ?? Array.Empty<TenantInfo>();
	}

	public Task<Guid?> GetTenantIdByDomainAsync(string domain)
	{
		var tenant = _tenants.FirstOrDefault(t =>
			t.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && t.IsActive);

		return Task.FromResult(tenant?.Id);
	}

	public Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId)
	{
		var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId && t.IsActive);
		return Task.FromResult(tenant);
	}

	public Task<TenantInfo[]> GetAllActiveTenantsAsync()
	{
		var activeTenants = _tenants.Where(t => t.IsActive).ToArray();
		return Task.FromResult(activeTenants);
	}
}
