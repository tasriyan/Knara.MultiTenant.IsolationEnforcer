using Multitenant.Enforcer.PerformanceMonitor;
using Multitenant.Enforcer.Resolvers;

namespace Multitenant.Enforcer;

/// <summary>
/// Configuration options for multi-tenant isolation.
/// </summary>
public class MultiTenantOptions
{
	/// <summary>
	/// The default tenant resolver type to use.
	/// </summary>
	public Type DefaultTenantResolver { get; set; } = typeof(JwtTenantResolver);

	/// <summary>
	/// Custom tenant resolver types for composite resolution.
	/// </summary>
	public Type[] CustomTenantResolvers { get; set; } = [];

	/// <summary>
	/// Whether to enable violation logging.
	/// </summary>
	public bool LogViolations { get; set; } = true;

	/// <summary>
	/// Whether to cache tenant resolution results.
	/// </summary>
	public bool CacheTenantResolution { get; set; } = true;

	/// <summary>
	/// Cache expiration time for tenant resolution in minutes.
	/// </summary>
	public int CacheExpirationMinutes { get; set; } = 5;

	/// <summary>
	/// Performance monitoring configuration.
	/// </summary>
	public PerformanceMonitoringOptions PerformanceMonitoring { get; set; } = new();

	/// <summary>
	/// Configures JWT-based tenant resolution.
	/// </summary>
	/// <param name="configure">Configuration action</param>
	/// <returns>The options for chaining</returns>
	public MultiTenantOptions UseJwtTenantResolver(Action<JwtTenantResolverOptions>? configure = null)
	{
		DefaultTenantResolver = typeof(JwtTenantResolver);

		var jwtOptions = new JwtTenantResolverOptions();
		configure?.Invoke(jwtOptions);

		return this;
	}

	/// <summary>
	/// Configures subdomain-based tenant resolution.
	/// </summary>
	/// <param name="configure">Configuration action</param>
	/// <returns>The options for chaining</returns>
	public MultiTenantOptions UseSubdomainTenantResolver(Action<SubdomainTenantResolverOptions>? configure = null)
	{
		DefaultTenantResolver = typeof(SubdomainTenantResolver);

		var subdomainOptions = new SubdomainTenantResolverOptions();
		configure?.Invoke(subdomainOptions);

		return this;
	}

	/// <summary>
	/// Configures composite tenant resolution with multiple strategies.
	/// </summary>
	/// <param name="resolverTypes">The resolver types to use</param>
	/// <returns>The options for chaining</returns>
	public MultiTenantOptions UseCompositeResolver(params Type[] resolverTypes)
	{
		CustomTenantResolvers = resolverTypes;
		return this;
	}
}
