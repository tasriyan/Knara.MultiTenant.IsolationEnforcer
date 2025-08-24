using Microsoft.AspNetCore.Http;

namespace Multitenant.Enforcer.TenantResolvers.Strategies;

public class NoOpTenantResolver : ITenantDomainValidator
{
	public async Task<bool> ValidateTenantDomainAsync(Guid tenantId, HttpContext context, CancellationToken cancellationToken)
	{
		return true;
	}
}