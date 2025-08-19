using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multitenant.Enforcer.Cache;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;

namespace Multitenant.Enforcer.DependencyInjection;

public class MultitenantIsolationBuilder(IServiceCollection services)
{
	public MultitenantIsolationBuilder WithTenantDomainCache<T>()
		where T : class, ITenantsCache
	{
		services.AddScoped<ITenantsCache, T>();
		services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantDomainCache<T>(Func<IServiceProvider, T> implementationFactory)
	where T : class, ITenantsCache
	{
		services.AddScoped<ITenantsCache>(implementationFactory);
		services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithInMemoryTenantCache()
	{
		services.AddMemoryCache(); 
		services.AddScoped<ITenantsCache, InMemoryTenantCache>();
		services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantsStore<T>()
		where T : class, IReadOnlyTenants
	{
		services.AddScoped<IReadOnlyTenants, T>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantsStore<T>(Func<IServiceProvider, T> implementationFactory)
	where T : class, IReadOnlyTenants
	{
		services.AddScoped<IReadOnlyTenants>(implementationFactory);
		return this;
	}

	public MultitenantIsolationBuilder WithJwtResolutionStrategy(Action<JwtTenantResolverOptions>? configure = null)
	{
		services.AddOptions<JwtTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = JwtTenantResolverOptions.DefaultOptions;
		});
		services.AddScoped<ITenantResolver, JwtTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithSubdomainResolutionStrategy(Action<SubdomainTenantResolverOptions>? configure = null)
	{
		services.AddOptions<SubdomainTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = SubdomainTenantResolverOptions.DefaultOptions;
		});
		services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithHeaderResolutionStrategy(Action<HeaderTenantResolverOptions>? configure = null)
	{
		services.AddOptions<HeaderTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = HeaderTenantResolverOptions.DefaultOptions;
		});
		services.AddScoped<ITenantResolver, HeaderTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithPathResolutionStrategy(Action<PathTenantResolverOptions>? configure = null)
	{
		services.AddOptions<PathTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = PathTenantResolverOptions.DefaultOptions;
		});
		services.AddScoped<ITenantResolver, PathTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithCustomResolutionStrategy<T>(Action<ITenantResolverOptions>? configure = null)
		where T : class, ITenantResolver
	{
		services.AddOptions<ITenantResolverOptions>().Configure(opts =>
		{
			configure?.Invoke(opts);
		});
		services.AddScoped<ITenantResolver, T>();
		return this;
	}
}
