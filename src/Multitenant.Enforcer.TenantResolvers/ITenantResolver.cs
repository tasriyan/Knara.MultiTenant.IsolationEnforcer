using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers;

public interface ITenantResolver
{
	Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken);
}