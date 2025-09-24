using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.AspNetCore.Http;

namespace Knara.MultiTenant.IsolationEnforcer.TenantResolvers;

public interface ITenantResolver
{
	Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken);
}