using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

public class SubdomainTenantResolver(
	ILogger<SubdomainTenantResolver> logger,
	ITenantLookupService tenantLookupService) : ITenantResolver
{
	private readonly ILogger<SubdomainTenantResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		// Check for system admin in JWT first
		if (context.User.HasClaim("role", "SystemAdmin"))
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

	private static string ExtractSubdomain(string host)
	{
		var parts = host.Split('.');

		if (parts.Length >= 3 && parts[0] != "www" && parts[0] != "localhost")
		{
			return parts[0];
		}

		return string.Empty;
	}
}
