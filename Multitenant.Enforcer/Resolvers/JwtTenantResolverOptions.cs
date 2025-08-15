namespace Multitenant.Enforcer.Resolvers;

public class JwtTenantResolverOptions
{
	public string TenantIdClaimType { get; set; } = "tenant_id";

	public string SystemAdminClaimType { get; set; } = "role";

	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";
}
