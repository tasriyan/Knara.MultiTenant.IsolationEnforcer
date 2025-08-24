using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers;

public interface ITenantDomainValidator
{
	Task<Guid?> ValidateTenantDomainAsync(HttpContext context, CancellationToken cancellationToken);
}
