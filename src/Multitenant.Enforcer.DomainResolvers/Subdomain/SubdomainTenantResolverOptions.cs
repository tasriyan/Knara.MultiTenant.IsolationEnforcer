namespace Multitenant.Enforcer.DomainResolvers;

public class SubdomainTenantResolverOptions: TenantResolverOptions
{
	public string[] ExcludedSubdomains { get; set; } = ["www", "api", "admin"];

	public static SubdomainTenantResolverOptions DefaultOptions { get; } = new SubdomainTenantResolverOptions();
}
