using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers.Strategies;

public class HeaderQueryTenantResolver(
	ILogger<HeaderQueryTenantResolver> logger,
	ITenantLookupService tenantLookupService,
	IOptions<HeaderQueryTenantResolverOptions> options) : ITenantResolver, ITenantDomainValidator
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly HeaderQueryTenantResolverOptions _options = options?.Value ?? HeaderQueryTenantResolverOptions.DefaultOptions;

	public async Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken)
	{
		logger.LogDebug("Resolving tenant from request header {Header} or query {Query}", context.Request.Headers, context.Request.QueryString);

		// Check for system admin in JWT first
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
			var tenantFromHeader = GetHeaderOrQueryStringValue(context);
			if (!Validators.IsValidTenantIdentifier(tenantFromHeader))
			{
				return false;
			}

			// If the tenant value is a GUID, validate it matches the provided tenantId
			if (Guid.TryParse(tenantFromHeader, out var parsedTenantId))
			{
				return parsedTenantId == tenantId;
			}

			// If the tenant value is a domain name, look it up and validate
			var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(tenantFromHeader!, cancellationToken);
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
		var tenant = GetHeaderOrQueryStringValue(context);
		if (!Validators.IsValidTenantIdentifier(tenant))
		{
			logger.LogDebug("No tenant found in request headers {Headers} or query parameters {Query}",
				context.Request.Headers, context.Request.QueryString);
			throw new TenantResolutionException(
				"Could not extract tenant from request",
				context.Request.Host.Host,
				"HeaderQuery");
		}

		if (Guid.TryParse(tenant, out var tenantId))
		{
			return await CreateTenantContextFromTenantId(tenantId, cancellationToken);
		}
		else
		{
			return await CreateTenantContextFromTenantName(tenant!, cancellationToken);
		}
	}

	private string? GetHeaderOrQueryStringValue(HttpContext context)
	{
		var tenant = context.TenantFromHeader(_options.IncludedHeaders);
		if (!string.IsNullOrWhiteSpace(tenant))
		{
			return tenant;
		}
		return context.TenantFromQuery(_options.IncludedQueryParameters);
	}

	private async Task<TenantContext> CreateTenantContextFromTenantId(Guid tenantId, CancellationToken cancellationToken)
	{
		var tenantInfo = await _tenantLookupService.GetTenantInfoAsync(tenantId, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for {tenantId}",
				tenantId.ToString(),
				"HeaderQuery");
		}
		logger.LogDebug("Tenant {TenantId} resolved from header or query string", tenantId);
		return TenantContext.ForTenant(tenantId, $"HeaderQuery:{tenantId}");
	}

	private async Task<TenantContext> CreateTenantContextFromTenantName(string tenant, CancellationToken cancellationToken)
	{
		var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(tenant, cancellationToken);
		if (tenantInfo == null || !tenantInfo.IsActive)
		{
			throw new TenantResolutionException(
				$"No active tenant found for {tenant}",
				tenant,
				"HeaderQuery");
		}
		logger.LogDebug("Tenant {TenantId} resolved from header or query string", tenantInfo.Id);
		return TenantContext.ForTenant(tenantInfo.Id, $"HeaderQuery:{tenant}");
	}
}

public class HeaderQueryTenantResolverOptions : TenantResolverOptions
{
	public string[] IncludedHeaders { get; set; } = ["X-Tenant-ID", "X-Tenant"];

	public string[] IncludedQueryParameters { get; set; } = ["tenant", "tenant_id", "tenantId", "tid"];

	public static HeaderQueryTenantResolverOptions DefaultOptions { get; } = new HeaderQueryTenantResolverOptions();
}
