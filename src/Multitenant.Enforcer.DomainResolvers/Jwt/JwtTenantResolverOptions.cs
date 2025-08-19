namespace Multitenant.Enforcer.DomainResolvers;

public class JwtTenantResolverOptions : TenantResolverOptions
{
	public string[] TenantIdClaimTypes { get; set; } = ["tenant_id", "tenantId", "tid"];

	public static JwtTenantResolverOptions DefaultOptions { get; } = new JwtTenantResolverOptions();
}
