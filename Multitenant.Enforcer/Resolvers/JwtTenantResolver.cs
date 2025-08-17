using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

public class JwtTenantResolver(ILogger<JwtTenantResolver> logger, IOptions<JwtTenantResolverOptions> options) : ITenantResolver
{
	private readonly JwtTenantResolverOptions _options = options?.Value ?? JwtTenantResolverOptions.DefaultOptions;

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		var user = context.User;

		// Check for system admin access
		foreach (var claimType in _options.SystemAdminClaimTypes)
		{
			if (user.HasClaim(c => c.Type == claimType && c.Value == _options.SystemAdminClaimValue))
			{
				logger.LogDebug("System admin access detected in JWT token");
				return TenantContext.SystemContext();
			}
		}

		// Look for tenant ID claim
		var tenantClaim = user.FindFirst(c => _options.TenantIdClaimTypes.Any(t => t == c.Type));
		if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
		{
			logger.LogDebug("Tenant {TenantId} resolved from JWT claim {ClaimType}",
				tenantId, tenantClaim.Type);
			return TenantContext.ForTenant(tenantId, "JWT");
		}

		throw new TenantResolutionException(
			"No tenant information found in JWT token",
			"JWT token missing tenant_id claim",
			"JWT");
	}
}
