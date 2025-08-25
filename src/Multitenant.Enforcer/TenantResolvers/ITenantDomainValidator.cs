using Microsoft.AspNetCore.Http;

namespace Multitenant.Enforcer.TenantResolvers;

public interface ITenantDomainValidator
{
	Task<bool> ValidateTenantDomainAsync(Guid tenantId, HttpContext context, CancellationToken cancellationToken);
}
