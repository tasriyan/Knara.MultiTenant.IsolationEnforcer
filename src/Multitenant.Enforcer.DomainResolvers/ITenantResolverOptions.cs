using System.Security.Claims;

namespace Multitenant.Enforcer.DomainResolvers;

public interface ITenantResolverOptions
{
}

public class TenantResolverOptions : ITenantResolverOptions
{
	public bool CacheMappings { get; set; } = true;

	public int CacheExpirationMinutes { get; set; } = 15;

	public string[] SystemAdminClaimTypes { get; set; } = ["role", ClaimTypes.Role];

	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";
}
