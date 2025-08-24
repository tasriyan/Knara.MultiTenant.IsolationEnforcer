using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers.Strategies;

public class NoOpTenantResolver : ITenantResolver
{
	public async Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken)
	{
		return TenantContext.ForTenant(Guid.Empty, "NoOp");
	}
}