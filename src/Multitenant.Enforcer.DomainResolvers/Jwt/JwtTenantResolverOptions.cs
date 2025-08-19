using System.Security.Claims;

namespace Multitenant.Enforcer.DomainResolvers;

public class JwtTenantResolverOptions
{
	public string[] TenantIdClaimTypes { get; set; } = ["tenant_id", "tenantId", "tid"];

	public string[] SystemAdminClaimTypes { get; set; } = ["role", ClaimTypes.Role];

	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";

	public static JwtTenantResolverOptions DefaultOptions { get; } = new JwtTenantResolverOptions();
}
