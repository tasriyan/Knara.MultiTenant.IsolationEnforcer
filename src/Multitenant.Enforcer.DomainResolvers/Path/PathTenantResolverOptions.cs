using System.Security.Claims;

namespace Multitenant.Enforcer.DomainResolvers;

public class PathTenantResolverOptions
{
	public string[] ExcludedPaths { get; set; } = ["api", "admin"];

	public bool CacheMappings { get; set; } = true;

	public int CacheExpirationMinutes { get; set; } = 15;

	public string[] SystemAdminClaimTypes { get; set; } = ["role", ClaimTypes.Role];

	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";

	public static PathTenantResolverOptions DefaultOptions { get; } = new PathTenantResolverOptions ();
}
