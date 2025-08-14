namespace Multitenant.Enforcer.Resolvers;

/// <summary>
/// Subdomain tenant resolver configuration.
/// </summary>
public class SubdomainTenantResolverOptions
{
	/// <summary>
	/// Domain suffixes to exclude from subdomain extraction.
	/// </summary>
	public string[] ExcludedSubdomains { get; set; } = { "www", "api", "admin" };

	/// <summary>
	/// Whether to cache domain-to-tenant mappings.
	/// </summary>
	public bool CacheMappings { get; set; } = true;

	/// <summary>
	/// Cache expiration time for domain mappings in minutes.
	/// </summary>
	public int CacheExpirationMinutes { get; set; } = 15;
}
