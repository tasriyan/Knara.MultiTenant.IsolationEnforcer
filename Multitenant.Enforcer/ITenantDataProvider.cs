using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer;

/// <summary>
/// Provides methods for retrieving tenant-related data, including active tenant IDs and information.
/// </summary>
/// <remarks>This interface is designed to support multi-tenant applications by enabling the retrieval of
/// tenant-specific data, such as active tenant IDs and detailed tenant information. Implementations of this interface
/// should ensure thread safety and handle cancellation tokens appropriately to support asynchronous
/// operations.</remarks>
public interface ITenantDataProvider
{
	Task<Guid?> GetActiveTenantIdByDomainAsync(string domain, CancellationToken cancellationToken);

	Task<Guid?> GetActiveTenantIdByDomainAsync(string domain,
				System.Linq.Expressions.Expression<Func<TenantEntity, bool>> predicate,
				CancellationToken cancellationToken);

	Task<TenantInfo?> GetActiveTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken);

	Task<TenantInfo[]> GetAllActiveTenantsAsync(CancellationToken cancellationToken);
}
