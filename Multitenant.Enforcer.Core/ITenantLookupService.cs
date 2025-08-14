namespace Multitenant.Enforcer.Core;

/// <summary>
/// Service for looking up tenant information from storage.
/// </summary>
public interface ITenantLookupService
{
	/// <summary>
	/// Gets the tenant ID for the specified domain.
	/// </summary>
	/// <param name="domain">The domain to lookup</param>
	/// <returns>The tenant ID if found, null otherwise</returns>
	Task<Guid?> GetTenantIdByDomainAsync(string domain);

	/// <summary>
	/// Gets tenant information by ID.
	/// </summary>
	/// <param name="tenantId">The tenant ID</param>
	/// <returns>Tenant information if found, null otherwise</returns>
	Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId);
}
