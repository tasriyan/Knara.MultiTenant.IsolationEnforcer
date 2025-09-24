using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knara.MultiTenant.IsolationEnforcer.TenantResolvers.Strategies;

public class SubdomainTenantResolver(
	ILogger<SubdomainTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<SubdomainTenantResolverOptions> options) : ITenantResolver, ITenantDomainValidator
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly SubdomainTenantResolverOptions _options = options?.Value ?? SubdomainTenantResolverOptions.DefaultOptions;

	public async Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken)
	{
		logger.LogDebug("Resolving tenant from request subdomain of {Host}", context.Request.Host.Host);

		if (context.IsUserASystemAdmin(_options.SystemAdminClaimTypes, _options.SystemAdminClaimValue))
		{
			logger.LogDebug("System admin access detected in user claims");
			return TenantContext.SystemContext();
		}

		return await ResolveTenantContext(context, cancellationToken);
	}

	public async Task<bool> ValidateTenantDomainAsync(Guid tenantId, HttpContext context, CancellationToken cancellationToken)
	{
		try
		{
			var tenantDomain = context.TenantFromSubdomain(_options.ExcludedSubdomains);
			if (string.IsNullOrWhiteSpace(tenantDomain))
			{
				return false;
			}

			var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(tenantDomain, cancellationToken);
			return tenantInfo?.Id == tenantId && tenantInfo.IsActive;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error validating tenant domain for tenant {TenantId}", tenantId);
			return false;
		}
	}

	private async Task<TenantContext> ResolveTenantContext(HttpContext context, CancellationToken cancellationToken)
	{
		var tenant = context.TenantFromSubdomain(_options.ExcludedSubdomains);

		if (string.IsNullOrWhiteSpace(tenant))
		{
			throw new TenantResolutionException(
				"Could not extract tenant from request",
				context.Request.Host.Host,
				"Subdomain");
		}

		var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(tenant, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for {tenant}",
				context.Request.Host.Host,
				"Subdomain");
		}

		logger.LogDebug("Tenant {TenantId} resolved from subdomain", tenantInfo.Id);

		return TenantContext.ForTenant(tenantInfo.Id, $"Subdomain:{tenant}");
	}
}

public class SubdomainTenantResolverOptions : TenantResolverOptions
{
	public string[] ExcludedSubdomains { get; set; } = ["www", "api", "admin"];

	public static SubdomainTenantResolverOptions DefaultOptions { get; } = new SubdomainTenantResolverOptions();
}


