using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

public interface ITenantResolver
{
	Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken);
}
