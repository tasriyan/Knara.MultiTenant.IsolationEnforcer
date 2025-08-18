using Multitenant.Enforcer.PerformanceMonitor;
using Multitenant.Enforcer.Resolvers;

namespace Multitenant.Enforcer;

public class MultiTenantOptions
{
	public Type DefaultTenantResolver { get; set; } = typeof(JwtTenantResolver);
	public Type[] CustomTenantResolvers { get; set; } = [];
	public bool LogViolations { get; set; } = true;
	public bool CacheTenantResolution { get; set; } = true;
	public int CacheExpirationMinutes { get; set; } = 5;
	public PerformanceMonitoringOptions PerformanceMonitoring { get; set; } = new();
	public SubdomainTenantResolverOptions SubdomainOptions { get; set; } = new();
	public JwtTenantResolverOptions JwtOptions { get; set; } = new();

	public static MultiTenantOptions DefaultOptions { get; } = new MultiTenantOptions();

	public MultiTenantOptions UseJwtTenantResolver(Action<JwtTenantResolverOptions>? configure = null)
	{
		DefaultTenantResolver = typeof(JwtTenantResolver);

		var jwtOptions = new JwtTenantResolverOptions();
		configure?.Invoke(jwtOptions);

		JwtOptions = jwtOptions;
		return this;
	}

	public MultiTenantOptions UseSubdomainTenantResolver(Action<SubdomainTenantResolverOptions>? configure = null)
	{
		DefaultTenantResolver = typeof(SubdomainTenantResolver);

		var subdomainOptions = new SubdomainTenantResolverOptions();
		configure?.Invoke(subdomainOptions);

		SubdomainOptions = subdomainOptions;
		return this;
	}

	public MultiTenantOptions UseCompositeResolver(params Type[] resolverTypes)
	{
		CustomTenantResolvers = resolverTypes;
		return this;
	}
}
