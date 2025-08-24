using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.TenantResolvers.Strategies;

public class JwtTenantResolver(
	ILogger<JwtTenantResolver> logger,
	ITenantDomainValidator domainValidator,
	ITenantLookupService tenantLookupService,
	IOptions<JwtTenantResolverOptions> options) : ITenantResolver
{
	private readonly ITenantLookupService _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
	private readonly JwtTenantResolverOptions _options = options?.Value ?? JwtTenantResolverOptions.DefaultOptions;

	public async Task<TenantContext> GetTenantContextAsync(HttpContext context, CancellationToken cancellationToken)
	{
		logger.LogDebug("Resolving tenant from JWT claims in request");

		// Check for system admin access
		if (context.IsUserASystemAdmin(_options.SystemAdminClaimTypes, _options.SystemAdminClaimValue))
		{
			logger.LogDebug("System admin access detected in user claims");
			return TenantContext.SystemContext();
		}

		// Look for tenant ID claim
		var claim = context.User.FindFirst(c => _options.TenantIdClaimTypes.Any(t => t == c.Type));
		if (claim == null)
		{
			throw new TenantResolutionException(
						"No tenant information found in JWT token",
						"JWT token missing tenant id claim",
						"JWT");
		}

		Guid? tenantId = await ExtractAndValidateTenantFromClaim(claim.Value, domainValidator, context, cancellationToken);
		if (tenantId.IsNullOrEmpty())
			throw new TenantResolutionException(
					"Invalid tenant id provided in claim or claim tenant is not active or not authorized to access this subdomain.",
					claim.Type,
					"JWT");

		logger.LogDebug("Tenant {TenantId} resolved from JWT claim {ClaimType}", tenantId, claim.Type);
		return TenantContext.ForTenant(tenantId.Value, "JWT");
	}


	private async Task<Guid?> ExtractAndValidateTenantFromClaim(string claimValue, ITenantDomainValidator domainValidator, HttpContext context, CancellationToken cancellationToken)
	{
		//if tenant is guid, then the id was already provided in claim, so return it
		if (Guid.TryParse(claimValue, out var tenantId))
		{
			var tenantInfo = await _tenantLookupService.GetTenantInfoAsync(tenantId, cancellationToken);
			if (tenantInfo != null && tenantInfo.IsActive)
			{
				var id = await domainValidator.ValidateTenantDomainAsync(context, cancellationToken);
				if (id == tenantInfo!.Id)
					return id;
			}
			return Guid.Empty;

		}

		//otherwise, look up the tenant by name(s)
		//assuming that the user has multiple tenants listed in claim, e.g. allowed_claims="acme.com,acme,contoso,contoso.com"
		var claims = claimValue.Split(separator: [',', ' ', ';'], options: StringSplitOptions.RemoveEmptyEntries);
		foreach (var claim in claims)
		{
			var tenantInfo = await _tenantLookupService.GetTenantInfoByDomainAsync(claim, cancellationToken);
			if (tenantInfo != null && tenantInfo.IsActive)
			{
				var id = await domainValidator.ValidateTenantDomainAsync(context, cancellationToken);
				if (id == tenantInfo!.Id)
					return id;
			}
		}

		return Guid.Empty;
	}
}

public class JwtTenantResolverOptions : TenantResolverOptions
{
	public string[] TenantIdClaimTypes { get; set; } = ["tenant_id", "tenantId", "tid"];

	public TenantDomainValidationMode RequestDomainResolver { get; set; } = TenantDomainValidationMode.ValidateAgainstSubdomain;

	public static JwtTenantResolverOptions DefaultOptions { get; } = new JwtTenantResolverOptions();
}

public enum TenantDomainValidationMode
{
	None,
	ValidateAgainstPath,
	ValidateAgainstHeaderOrQuery,
	ValidateAgainstSubdomain
}
