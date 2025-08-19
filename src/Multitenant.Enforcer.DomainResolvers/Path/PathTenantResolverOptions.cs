namespace Multitenant.Enforcer.DomainResolvers;

public class PathTenantResolverOptions: TenantResolverOptions
{
	public string[] ExcludedPaths { get; set; } = ["api", "admin"];

	public static PathTenantResolverOptions DefaultOptions { get; } = new PathTenantResolverOptions ();
}
