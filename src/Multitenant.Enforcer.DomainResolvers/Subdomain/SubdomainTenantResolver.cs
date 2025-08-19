using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.DomainResolvers;

public class SubdomainTenantResolver(
	ILogger<SubdomainTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<SubdomainTenantResolverOptions> options) : ITenantResolver
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly SubdomainTenantResolverOptions _options = options?.Value ?? SubdomainTenantResolverOptions.DefaultOptions;

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		if (context.IsUserASystemAdmin(_options.SystemAdminClaimTypes, _options.SystemAdminClaimValue))
		{
			logger.LogDebug("System admin access detected in user claims");
			return TenantContext.SystemContext();
		}

		var host = context.Request.Host.Host;
		var subdomain = context.ExtractSubdomainFromDomain(_options.ExcludedSubdomains);

		if (string.IsNullOrWhiteSpace(subdomain))
		{
			throw new TenantResolutionException(
				"No subdomain found in request",
				host,
				"Subdomain");
		}

		var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(subdomain, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for domain: {subdomain}",
				host,
				"Subdomain");
		}

		logger.LogDebug("Tenant {TenantId} resolved from subdomain {Subdomain}",
			tenantInfo.Id, subdomain);

		return TenantContext.ForTenant(tenantInfo.Id, $"Subdomain:{subdomain}");
	}
}
