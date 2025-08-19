using System.Security.Claims;

namespace Multitenant.Enforcer.DomainResolvers;

public class HeaderTenantResolverOptions
{
	public string[] IncludedHeaders { get; set; } = ["X-Tenant-ID", "X-Tenant" ];

	public string[] IncludedQueryParameters { get; set; } = ["tenant", "tenant_id", "tenantId", "tid"];

	public bool CacheMappings { get; set; } = true;

	public int CacheExpirationMinutes { get; set; } = 15;

	public string[] SystemAdminClaimTypes { get; set; } = ["role", ClaimTypes.Role];

	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";

	public static HeaderTenantResolverOptions DefaultOptions { get; } = new HeaderTenantResolverOptions();
}
