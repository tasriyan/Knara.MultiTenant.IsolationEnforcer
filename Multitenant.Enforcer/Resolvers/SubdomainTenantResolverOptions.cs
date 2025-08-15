namespace Multitenant.Enforcer.Resolvers;

public class SubdomainTenantResolverOptions
{
	public string[] ExcludedSubdomains { get; set; } = { "www", "api", "admin" };

	public bool CacheMappings { get; set; } = true;

	public int CacheExpirationMinutes { get; set; } = 15;
}
