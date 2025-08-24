using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multitenant.Enforcer.Cache;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.PerformanceMonitor;
using Multitenant.Enforcer.TenantResolvers;
using Multitenant.Enforcer.TenantResolvers.Strategies;

namespace Multitenant.Enforcer.DependencyInjection;

public class MultitenantIsolationBuilder(IServiceCollection services)
{
	private readonly IServiceCollection _services = services ?? throw new ArgumentNullException(nameof(services));

	#region Tenant Cache Configuration

	public MultitenantIsolationBuilder WithTenantDomainCache<T>()
		where T : class, ITenantsCache
	{
		_services.AddScoped<ITenantsCache, T>();
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	/// <summary>
	/// Configures a custom tenant cache implementation with factory.
	/// </summary>
	public MultitenantIsolationBuilder WithTenantDomainCache<T>(Func<IServiceProvider, T> implementationFactory)
		where T : class, ITenantsCache
	{
		_services.AddScoped<ITenantsCache>(implementationFactory);
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	/// <summary>
	/// Configures in-memory tenant caching (default and recommended for most scenarios).
	/// </summary>
	public MultitenantIsolationBuilder WithInMemoryTenantCache()
	{
		_services.AddMemoryCache(); 
		_services.AddScoped<ITenantsCache, InMemoryTenantCache>();
		_services.TryAddScoped<ITenantCacheManager, TenantCacheManager>();
		return this;
	}

	#endregion

	#region Tenant Store Configuration

	public MultitenantIsolationBuilder WithTenantsStore<T>()
		where T : class, ITenantStore
	{
		_services.AddScoped<ITenantStore, T>();
		return this;
	}

	/// <summary>
	/// Configures the tenant store implementation with factory.
	/// </summary>
	public MultitenantIsolationBuilder WithTenantsStore<T>(Func<IServiceProvider, T> implementationFactory)
		where T : class, ITenantStore
	{
		_services.AddScoped<ITenantStore>(implementationFactory);
		return this;
	}

	#endregion

	#region Tenant Resolution Strategies

	public MultitenantIsolationBuilder WithJwtResolutionStrategy(Action<JwtTenantResolverOptions>? configure = null)
	{
		services.AddOptions<JwtTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = JwtTenantResolverOptions.DefaultOptions;
		});


		_services.AddScoped<DomainValidationResolverFactory>();
		_services.AddScoped<ITenantResolver, JwtTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithJwtResolutionStrategy(JwtTenantResolverOptions? options = null)
	{
		if (options == null)
			options = JwtTenantResolverOptions.DefaultOptions;

		if (options.RequestDomainResolver == JwtRequestDomainResolvers.Path)
		{
			_services.AddScoped<PathTenantResolver>();
		}
		else if (options.RequestDomainResolver == JwtRequestDomainResolvers.HeaderOrQuery)
		{
			_services.AddScoped<HeaderQueryTenantResolver>();
		}
		else if (options.RequestDomainResolver == JwtRequestDomainResolvers.Subdomain)
		{
			_services.AddScoped<SubdomainTenantResolver>();
		}
		else
		{
			_services.AddScoped<NoOpTenantResolver>();
		}

		_services.AddScoped<DomainValidationResolverFactory>();
		_services.AddScoped<ITenantResolver, JwtTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithSubdomainResolutionStrategy(Action<SubdomainTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<SubdomainTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = SubdomainTenantResolverOptions.DefaultOptions;
		});
		_services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithHeaderResolutionStrategy(Action<HeaderQueryTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<HeaderQueryTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = HeaderQueryTenantResolverOptions.DefaultOptions;
		});
		_services.AddScoped<ITenantResolver, HeaderQueryTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithPathResolutionStrategy(Action<PathTenantResolverOptions>? configure = null)
	{
		_services.AddOptions<PathTenantResolverOptions>().Configure(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
			else
				opts = PathTenantResolverOptions.DefaultOptions;
		});
		_services.AddScoped<ITenantResolver, PathTenantResolver>();
		return this;
	}

	public MultitenantIsolationBuilder WithCustomResolutionStrategy<T>(Action<ITenantResolverOptions>? configure = null)
		where T : class, ITenantResolver
	{
		_services.AddOptions<ITenantResolverOptions>().Configure(opts =>
		{
			configure?.Invoke(opts);
		});
		_services.AddScoped<ITenantResolver, T>();
		return this;
	}

	public MultitenantIsolationBuilder WithCompositeResolutionStrategy(params Type[] resolverTypes)
	{
		foreach (var resolverType in resolverTypes)
		{
			if (!typeof(ITenantResolver).IsAssignableFrom(resolverType))
				throw new ArgumentException($"Type {resolverType.Name} must implement ITenantResolver", nameof(resolverTypes));
			
			_services.AddScoped(typeof(ITenantResolver), resolverType);
		}
		return this;
	}

	#endregion

	#region Performance Monitoring Configuration

	/// <summary>
	/// Configures performance monitoring options. 
	/// Note: Performance monitoring is MANDATORY but can be configured.
	/// </summary>
	public MultitenantIsolationBuilder WithPerformanceMonitoring(Action<PerformanceMonitoringOptions>? configure = null)
	{
		_services.Configure<PerformanceMonitoringOptions>(opts =>
		{
			if (configure != null)
				configure.Invoke(opts);
		});
		
		return this;
	}

	/// <summary>
	/// Replaces the default metrics collector with a custom implementation.
	/// Performance monitoring remains mandatory but allows custom metrics collection.
	/// </summary>
	public MultitenantIsolationBuilder WithCustomMetricsCollector<T>()
		where T : class, ITenantMetricsCollector
	{
		_services.Replace(ServiceDescriptor.Scoped<ITenantMetricsCollector, T>());
		return this;
	}

	/// <summary>
	/// Replaces the default metrics collector with a custom factory.
	/// </summary>
	public MultitenantIsolationBuilder WithCustomMetricsCollector<T>(Func<IServiceProvider, T> implementationFactory)
		where T : class, ITenantMetricsCollector
	{
		_services.Replace(ServiceDescriptor.Scoped<ITenantMetricsCollector>(implementationFactory));
		return this;
	}

	/// <summary>
	/// Replaces the entire performance monitor with a custom implementation.
	/// Security monitoring (violations, audit trails) must still be implemented.
	/// </summary>
	public MultitenantIsolationBuilder WithCustomPerformanceMonitor<T>()
		where T : class, ITenantPerformanceMonitor
	{
		_services.Replace(ServiceDescriptor.Scoped<ITenantPerformanceMonitor, T>());
		return this;
	}

	/// <summary>
	/// Replaces both the performance monitor and metrics collector.
	/// </summary>
	public MultitenantIsolationBuilder WithCustomPerformanceMonitoring<TMonitor, TCollector>(
		Action<PerformanceMonitoringOptions>? configure = null)
		where TMonitor : class, ITenantPerformanceMonitor
		where TCollector : class, ITenantMetricsCollector
	{
		if (configure != null)
		{
			_services.Configure<PerformanceMonitoringOptions>(configure);
		}
		
		_services.Replace(ServiceDescriptor.Scoped<ITenantMetricsCollector, TCollector>());
		_services.Replace(ServiceDescriptor.Scoped<ITenantPerformanceMonitor, TMonitor>());
		return this;
	}

	#endregion

	#region Validation and Utilities

	/// <summary>
	/// Validates the current configuration and ensures all required services are registered.
	/// Called automatically but can be called explicitly for early validation.
	/// </summary>
	public MultitenantIsolationBuilder ValidateConfiguration()
	{
		// Ensure at least one tenant resolver is configured
		var hasResolver = _services.Any(s => s.ServiceType == typeof(ITenantResolver));
		if (!hasResolver)
		{
			throw new InvalidOperationException(
				"No tenant resolver configured. Use one of: WithJwtResolutionStrategy(), WithSubdomainResolutionStrategy(), WithHeaderResolutionStrategy(), WithPathResolutionStrategy(), or WithCustomResolutionStrategy<T>()");
		}

		// Ensure tenant store is configured
		var hasTenantStore = _services.Any(s => s.ServiceType == typeof(ITenantStore));
		if (!hasTenantStore)
		{
			throw new InvalidOperationException(
				"No tenant store configured. Use WithTenantsStore<T>() to configure a tenant store implementation.");
		}

		// Ensure tenant cache is configured (optional but recommended)
		var hasTenantCache = _services.Any(s => s.ServiceType == typeof(ITenantsCache));
		if (!hasTenantCache)
		{
			// Auto-configure in-memory cache if none specified
			WithInMemoryTenantCache();
		}

		return this;
	}

	/// <summary>
	/// Builds and validates the final configuration.
	/// </summary>
	public IServiceCollection Build()
	{
		ValidateConfiguration();
		return _services;
	}

	#endregion

	#region Extension Points

	/// <summary>
	/// Allows custom service registrations within the isolation builder context.
	/// </summary>
	public MultitenantIsolationBuilder ConfigureServices(Action<IServiceCollection> configure)
	{
		configure?.Invoke(_services);
		return this;
	}

	/// <summary>
	/// Registers a custom service with the DI container.
	/// </summary>
	public MultitenantIsolationBuilder AddService<TInterface, TImplementation>()
		where TInterface : class
		where TImplementation : class, TInterface
	{
		_services.AddScoped<TInterface, TImplementation>();
		return this;
	}

	/// <summary>
	/// Registers a custom service with a factory.
	/// </summary>
	public MultitenantIsolationBuilder AddService<TInterface>(Func<IServiceProvider, TInterface> factory)
		where TInterface : class
	{
		_services.AddScoped(factory);
		return this;
	}

	#endregion
}

