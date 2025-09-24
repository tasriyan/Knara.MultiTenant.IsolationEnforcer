using Microsoft.AspNetCore.Http;

namespace Knara.MultiTenant.IsolationEnforcer.TenantResolvers;

public interface ITenantDomainValidator
{
	Task<bool> ValidateTenantDomainAsync(Guid tenantId, HttpContext context, CancellationToken cancellationToken);
}
