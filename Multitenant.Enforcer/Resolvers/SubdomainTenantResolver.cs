using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using System.Security.Claims;

namespace Multitenant.Enforcer.Resolvers;

public class SubdomainTenantResolver(
	ILogger<SubdomainTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	SubdomainTenantResolverOptions options) : ITenantResolver
{
	private readonly ILogger<SubdomainTenantResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		// Check for system admin in JWT first
		if (context.User.HasClaim(ClaimTypes.Role, "SystemAdmin") || context.User.HasClaim("role", "SystemAdmin"))
		{
			return TenantContext.SystemContext("SystemAdmin-JWT");
		}

		var host = context.Request.Host.Host;
		var subdomain = ExtractSubdomain(host);

		if (string.IsNullOrWhiteSpace(subdomain))
		{
			throw new TenantResolutionException(
				"No subdomain found in request",
				host,
				"Subdomain");
		}

		var tenantId = await _tenantLookupService.GetTenantIdByDomainAsync(subdomain, cancellationToken);
		if (tenantId == null)
		{
			throw new TenantResolutionException(
				$"No active tenant found for domain: {subdomain}",
				subdomain,
				"Subdomain");
		}

		_logger.LogDebug("Tenant {TenantId} resolved from subdomain {Subdomain}",
			tenantId, subdomain);

		return TenantContext.ForTenant(tenantId.Value, $"Subdomain:{subdomain}");
	}

	// Assuming the subdomain is the first part of the host
	//		https://acme-corp.yourapp.com
	//		https://www.globex.yourapp.com  
	//		https://admin.initech.yourapp.com
	//		https://yourapp.com (no subdomain)
	//		http://localhost:5000 (no subdomain)
	private string ExtractSubdomain(string host)
	{
		var parts = host.Split('.');

		// Need at least 3 parts for subdomain: subdomain.domain.com
		if (parts.Length < 3) return string.Empty;

		// Check if first part should be skipped (www, admin, etc.)
		if (options.ExcludedSubdomains.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
		{
			// Use second part as tenant: www.globex.yourapp.com -> "globex"
			return parts.Length >= 4 ? parts[1] : string.Empty;
		}

		// Use first part as tenant: acme-corp.yourapp.com -> "acme-corp"
		return parts[0];
	}
}
