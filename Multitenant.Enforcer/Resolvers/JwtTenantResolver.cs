using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

/// <summary>
/// Tenant resolver that extracts tenant information from JWT token claims.
/// </summary>
public class JwtTenantResolver(ILogger<JwtTenantResolver> logger) : ITenantResolver
{
	private readonly ILogger<JwtTenantResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		var user = context.User;

		// Check for system admin access first
		if (user.HasClaim("role", "SystemAdmin") || user.HasClaim("system_access", "true"))
		{
			_logger.LogDebug("System admin access detected in JWT token");
			return TenantContext.SystemContext("JWT-System");
		}

		// Look for tenant ID in standard claims
		var tenantClaim = user.FindFirst("tenant_id") ??
						 user.FindFirst("tenantId") ??
						 user.FindFirst("tid");

		if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
		{
			_logger.LogDebug("Tenant {TenantId} resolved from JWT claim {ClaimType}",
				tenantId, tenantClaim.Type);
			return TenantContext.ForTenant(tenantId, "JWT");
		}

		throw new TenantResolutionException(
			"No tenant information found in JWT token",
			"JWT token missing tenant_id claim",
			"JWT");
	}
}
