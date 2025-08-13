// MultiTenant.Enforcer.AspNetCore/ServiceCollectionExtensions.cs
using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MultiTenant.Enforcer.Core;
using MultiTenant.Enforcer.EntityFramework;

namespace MultiTenant.Enforcer.AspNetCore
{
    /// <summary>
    /// Extension methods for configuring multi-tenant isolation services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds multi-tenant isolation enforcement services to the service collection.
        /// </summary>
        /// <typeparam name="TDbContext">The DbContext type that inherits from TenantDbContext</typeparam>
        /// <param name="services">The service collection</param>
        /// <param name="configure">Optional configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMultiTenantIsolation<TDbContext>(
            this IServiceCollection services,
            Action<MultiTenantOptions>? configure = null)
            where TDbContext : TenantDbContext
        {
            var options = new MultiTenantOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);

            // Register core services
            services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
            services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();

            // Register tenant resolver based on configuration
            RegisterTenantResolver(services, options);

            // Register performance monitoring if enabled
            if (options.PerformanceMonitoring.Enabled)
            {
                services.AddScoped<ITenantPerformanceMonitor, TenantPerformanceMonitor>();
            }

            // Auto-register tenant repositories for all ITenantIsolated entities
            RegisterTenantRepositories<TDbContext>(services);

            // Register tenant lookup service if using subdomain resolution
            if (options.DefaultTenantResolver == typeof(SubdomainTenantResolver))
            {
                services.TryAddScoped<ITenantLookupService, CachedTenantLookupService>();
            }

            return services;
        }

        /// <summary>
        /// Adds multi-tenant isolation middleware to the application pipeline.
        /// Must be called before UseAuthentication().
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseMultiTenantIsolation(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TenantContextMiddleware>();
        }

        private static void RegisterTenantResolver(IServiceCollection services, MultiTenantOptions options)
        {
            if (options.DefaultTenantResolver == typeof(SubdomainTenantResolver))
            {
                services.AddScoped<ITenantResolver, SubdomainTenantResolver>();
            }
            else if (options.DefaultTenantResolver == typeof(JwtTenantResolver))
            {
                services.AddScoped<ITenantResolver, JwtTenantResolver>();
            }
            else if (options.CustomTenantResolvers.Any())
            {
                // Register composite resolver with custom resolvers
                foreach (var resolverType in options.CustomTenantResolvers)
                {
                    services.AddScoped(typeof(ITenantResolver), resolverType);
                }

                services.AddScoped<ITenantResolver>(provider =>
                {
                    var resolvers = provider.GetServices<ITenantResolver>()
                        .Where(r => r.GetType() != typeof(CompositeTenantResolver))
                        .ToArray();

                    var logger = provider.GetRequiredService<ILogger<CompositeTenantResolver>>();
                    return new CompositeTenantResolver(resolvers, logger);
                });
            }
            else
            {
                // Default to JWT resolver
                services.AddScoped<ITenantResolver, JwtTenantResolver>();
            }
        }

        private static void RegisterTenantRepositories<TDbContext>(IServiceCollection services)
            where TDbContext : TenantDbContext
        {
            var dbContextType = typeof(TDbContext);
            var assembly = dbContextType.Assembly;

            // Find all ITenantIsolated entities in the same assembly as the DbContext
            var entityTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ITenantIsolated).IsAssignableFrom(t))
                .ToList();

            foreach (var entityType in entityTypes)
            {
                // Register ITenantRepository<TEntity>
                var repositoryInterface = typeof(ITenantRepository<>).MakeGenericType(entityType);
                var repositoryImplementation = typeof(TenantRepository<,>).MakeGenericType(entityType, dbContextType);

                services.AddScoped(repositoryInterface, repositoryImplementation);

                // Also register the specific DbContext variant
                var specificRepositoryInterface = typeof(ITenantRepository<,>).MakeGenericType(entityType, dbContextType);
                services.AddScoped(specificRepositoryInterface, repositoryImplementation);
            }

            // Also scan for additional assemblies if specified
            var additionalAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetReferencedAssemblies().Any(ra => ra.Name == "MultiTenant.Enforcer.Core"))
                .ToList();

            foreach (var assembly2 in additionalAssemblies)
            {
                var additionalEntityTypes = assembly2.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ITenantIsolated).IsAssignableFrom(t))
                    .Where(t => !entityTypes.Contains(t))
                    .ToList();

                foreach (var entityType in additionalEntityTypes)
                {
                    var repositoryInterface = typeof(ITenantRepository<>).MakeGenericType(entityType);
                    var repositoryImplementation = typeof(TenantRepository<,>).MakeGenericType(entityType, dbContextType);

                    services.TryAddScoped(repositoryInterface, repositoryImplementation);
                }
            }
        }
    }

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
        public Type[] CustomTenantResolvers { get; set; } = Array.Empty<Type>();

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

    /// <summary>
    /// Performance monitoring configuration.
    /// </summary>
    public class PerformanceMonitoringOptions
    {
        /// <summary>
        /// Whether performance monitoring is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Threshold in milliseconds for logging slow queries.
        /// </summary>
        public int SlowQueryThresholdMs { get; set; } = 1000;

        /// <summary>
        /// Whether to log query execution plans.
        /// </summary>
        public bool LogQueryPlans { get; set; } = false;

        /// <summary>
        /// Whether to collect metrics for performance dashboards.
        /// </summary>
        public bool CollectMetrics { get; set; } = true;
    }

    /// <summary>
    /// JWT tenant resolver configuration.
    /// </summary>
    public class JwtTenantResolverOptions
    {
        /// <summary>
        /// The claim type that contains the tenant ID.
        /// </summary>
        public string TenantIdClaimType { get; set; } = "tenant_id";

        /// <summary>
        /// The claim type that indicates system admin access.
        /// </summary>
        public string SystemAdminClaimType { get; set; } = "role";

        /// <summary>
        /// The claim value that indicates system admin access.
        /// </summary>
        public string SystemAdminClaimValue { get; set; } = "SystemAdmin";
    }

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
}
