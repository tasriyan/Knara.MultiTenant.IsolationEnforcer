namespace Multitenant.Enforcer.DomainResolvers;

public class HeaderTenantResolverOptions : TenantResolverOptions
{
	public string[] IncludedHeaders { get; set; } = ["X-Tenant-ID", "X-Tenant" ];

	public string[] IncludedQueryParameters { get; set; } = ["tenant", "tenant_id", "tenantId", "tid"];

	public static HeaderTenantResolverOptions DefaultOptions { get; } = new HeaderTenantResolverOptions();
}
