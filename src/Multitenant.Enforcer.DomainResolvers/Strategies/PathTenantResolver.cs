using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers.Strategies;

public class PathTenantResolver(
	ILogger<PathTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<PathTenantResolverOptions> options) : ITenantResolver, ITenantDomainValidator
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly PathTenantResolverOptions _options = options?.Value ?? PathTenantResolverOptions.DefaultOptions;
	public async Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken)
	{
		logger.LogDebug("Resolving tenant from request path {Path}", context.Request.Path);

		// Check for system admin in JWT first
		if (context.IsUserASystemAdmin(_options.SystemAdminClaimTypes, _options.SystemAdminClaimValue))
		{
			logger.LogDebug("System admin access detected in user claims");
			return TenantContext.SystemContext();
		}


		return await ResolveTenantContext(context, cancellationToken);
	}

	public async Task<Guid?> ValidateTenantDomainAsync(HttpContext context, CancellationToken cancellationToken)
	{
		var tenantContext = await ResolveTenantContext(context, cancellationToken);
		return tenantContext!.TenantId;
	}

	private async Task<TenantContext> ResolveTenantContext(HttpContext context, CancellationToken cancellationToken)
	{
		var tenant = context.TenantFromPath(_options.ExcludedPaths);

		if (string.IsNullOrWhiteSpace(tenant))
		{
			logger.LogDebug("No tenant found in request path {Path}", context.Request.Path);
			throw new TenantResolutionException(
				"Could not extract tenant from request",
				context.Request.Host.Host,
				"Path");
		}
		return await CreateTenantContext(tenant!, cancellationToken);
	}
	private async Task<TenantContext> CreateTenantContext(string tenant, CancellationToken cancellationToken)
	{
		var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(tenant, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for {tenant}",
				tenant,
				"Path");
		}
		logger.LogDebug("Tenant {TenantId} resolved from path", tenantInfo.Id);
		return TenantContext.ForTenant(tenantInfo.Id, $"Path:{tenant}");
	}
}

public class PathTenantResolverOptions : TenantResolverOptions
{
	public string[] ExcludedPaths { get; set; } = ["api", "admin"];

	public static PathTenantResolverOptions DefaultOptions { get; } = new PathTenantResolverOptions();
}
