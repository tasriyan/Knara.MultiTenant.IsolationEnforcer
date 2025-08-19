using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multitenant.Enforcer.Cache;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;

namespace Multitenant.Enforcer.DependencyInjection;

public class MultitenantIsolationBuilder
{
	private readonly IServiceCollection _services;

	public MultitenantIsolationBuilder(IServiceCollection services)
	{
		_services = services;
	}

	public MultitenantIsolationBuilder WithTenantDomainCache<T>()
		where T : class, ITenantCache
	{
		_services.AddScoped<ITenantCache, T>();
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantDomainCache<T>(Func<IServiceProvider, T> implementationFactory)
	where T : class, ITenantCache
	{
		_services.AddScoped<ITenantCache>(implementationFactory);
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithInMemoryTenantCache()
	{
		_services.AddMemoryCache(); 
		_services.AddScoped<ITenantCache, InMemoryTenantCache>();
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantStore<T>()
		where T : class, ITenantStore
	{
		_services.AddScoped<ITenantStore, T>();
		return this;
	}

	public MultitenantIsolationBuilder WithTenantStore<T>(Func<IServiceProvider, T> implementationFactory)
	where T : class, ITenantStore
	{
		_services.AddScoped<ITenantStore>(implementationFactory);
		return this;
	}

	public MultitenantIsolationBuilder WithJwtResolutionStrategy(Action<JwtTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<JwtTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = JwtTenantResolverOptions.DefaultOptions;
		});
		_services.AddScoped<ITenantResolver, JwtTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithSubdomainResolutionStrategy(Action<SubdomainTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<SubdomainTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
		});
		_services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithHeaderResolutionStrategy(Action<HeaderTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<HeaderTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
		});
		_services.AddScoped<ITenantResolver, HeaderTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithPathResolutionStrategy(Action<PathTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<PathTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
		});
		_services.AddScoped<ITenantResolver, PathTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithCustomResolutionStrategy<T>(Action<IResolverOptions>? configure = null)
		where T : class, ITenantResolver
	{
		_services.AddOptions<IResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
		});
		_services.AddScoped<ITenantResolver, T>();
		return this;
	}
}
