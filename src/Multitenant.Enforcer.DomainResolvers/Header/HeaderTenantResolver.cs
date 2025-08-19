using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.DomainResolvers;

public class HeaderTenantResolver(
	ILogger<HeaderTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<HeaderTenantResolverOptions> options) : ITenantResolver
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly HeaderTenantResolverOptions _options = options?.Value ?? HeaderTenantResolverOptions.DefaultOptions;

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
		var subdomain = ResolveHeader(context);
		if (string.IsNullOrWhiteSpace(subdomain))
		{
			logger.LogDebug("No subdomain found in request headers or query parameters");
			throw new TenantResolutionException(
				"No subdomain found in request",
				host,
				"Header");
		}

		if (Guid.TryParse(subdomain, out var parsedTenantId))
		{
			return await ResolveTenantFromId(parsedTenantId, cancellationToken);
		}
		else
		{
			return await ResolveTenantFromDomain(subdomain, cancellationToken);
		}
	}

	private string? ResolveHeader(HttpContext context)
	{
		var result = context.ExtractSubdomainFromHeader(_options.IncludedHeaders);
		if (!string.IsNullOrWhiteSpace(result))
		{
			return result;
		}
		return context.ExtractSubdomaintFromQuery(_options.IncludedQueryParameters);
	}

	private async Task<TenantContext> ResolveTenantFromId(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfo = await _tenantLookupService.GetTenantInfoAsync(tenantId, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for ID: {tenantId}",
				tenantId.ToString(),
				"Header");
		}
		logger.LogDebug("Tenant {TenantId} resolved from header ID", tenantId);
		return TenantContext.ForTenant(tenantId, $"Header:{tenantId}");
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
