using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.DomainResolvers;

public class PathTenantResolver(
	ILogger<PathTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<PathTenantResolverOptions> options) : ITenantResolver
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly PathTenantResolverOptions _options = options?.Value ?? PathTenantResolverOptions.DefaultOptions;
	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		var user = context.User;

		// Check for system admin in JWT first
		if (context.IsUserASystemAdmin(_options.SystemAdminClaimTypes, _options.SystemAdminClaimValue))
		{
			logger.LogDebug("System admin access detected in user claims");
			return TenantContext.SystemContext();
		}

		var host = context.Request.Host.Host;
		var subdomain = context.ExtractSubdomainFromPath(_options.ExcludedPaths);

		if (string.IsNullOrWhiteSpace(subdomain))
		{
			throw new TenantResolutionException(
				"No subdomain found in request",
				host,
				"Subdomain");
		}

		return await ResolveTenantFromDomain(subdomain, cancellationToken);
	}

	private async Task<TenantContext> ResolveTenantFromDomain(string domain, CancellationToken cancellationToken)
	{
		var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(domain, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for domain: {domain}",
				domain,
				"Header");
		}
		logger.LogDebug("Tenant {TenantId} resolved from header domain", tenantInfo.Id);
		return TenantContext.ForTenant(tenantInfo.Id, $"Header:{domain}");
	}
}
