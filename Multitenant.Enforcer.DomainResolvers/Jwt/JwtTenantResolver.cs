using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.DomainResolvers;

public class JwtTenantResolver(
	ILogger<JwtTenantResolver> logger,
	ITenantResolver subdomainResolver,
	IOptions<JwtTenantResolverOptions> options) : ITenantResolver
{
	private readonly ITenantResolver _subdomainResolver = subdomainResolver ?? throw new ArgumentNullException(nameof(subdomainResolver));
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
		if (tenantClaim == null || !Guid.TryParse(tenantClaim.Value, out var claimTenantId))
		{
			throw new TenantResolutionException(
						"No tenant information found in JWT token",
						"JWT token missing tenant_id claim",
						"JWT");
		}

		if (!await HasRightsToDomain(claimTenantId, context, cancellationToken))
		{
			throw new TenantResolutionException(
				"Tenant is not authorized to access this subdomain",
				context.Request.Host.Host,
				"Subdomain");
		}

		logger.LogDebug("Tenant {TenantId} resolved from JWT claim {ClaimType}",
			claimTenantId, tenantClaim.Type);
		return TenantContext.ForTenant(claimTenantId, "JWT");
	}

	private async Task<bool> HasRightsToDomain(Guid claimTenantId, HttpContext context, CancellationToken cancellationToken)
	{
		var tenantContext = await _subdomainResolver.ResolveTenantAsync(context, cancellationToken);
		return claimTenantId == tenantContext.TenantId;

	}
}
