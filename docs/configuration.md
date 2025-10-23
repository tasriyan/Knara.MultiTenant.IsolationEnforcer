# Configuration Guide

This guide covers configuration options for the multi-tenant isolation library. The library uses an opinionated design to prevent common tenant isolation errors.

## Basic Configuration

Minimal configuration requires a tenant store and resolution strategy:

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy();
```

## Core Options

### Initial Setup

```csharp
services.AddMultiTenantIsolation(options =>
{
    options.CacheTenantResolution = true;
    options.CacheExpirationMinutes = 30;
});
```

Default options are applied if no configuration is provided.

## Tenant Cache

### In-Memory Cache

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
```

Recommended for most scenarios. Fast and works out-of-the-box.

### Custom Cache Implementation

```csharp
// Simple registration
services.AddMultiTenantIsolation()
    .WithTenantDomainCache<RedisTenantCache>()

// Factory-based registration
services.AddMultiTenantIsolation()
    .WithTenantDomainCache<RedisTenantCache>(provider => 
        new RedisTenantCache(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            provider.GetRequiredService<ILogger<RedisTenantCache>>()
        ))
```

## Tenant Store

### Simple Registration

```csharp
services.AddMultiTenantIsolation()
    .WithTenantsStore<EFCoreTenantStore>()
```

### Factory-Based Registration

```csharp
services.AddMultiTenantIsolation()
    .WithTenantsStore<EFCoreTenantStore>(provider =>
        new EFCoreTenantStore(
            provider.GetRequiredService<MyDbContext>(),
            provider.GetRequiredService<ILogger<EFCoreTenantStore>>()
        ))
```

The tenant store must implement `ITenantStore`. This is mandatory; the library throws an exception if no tenant store is configured.

## Tenant Resolution Strategies

### Subdomain-Based

```csharp
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.CacheMappings = true;
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
        options.SystemAdminClaimValue = "SystemAdmin";
    })
```

Use case: `tenant1.yourapp.com`, `tenant2.yourapp.com`

### JWT-Based

```csharp
services.AddMultiTenantIsolation()
    .WithJwtResolutionStrategy(options =>
    {
        options.TenantClaimType = "tenant_id";
        options.RequireHttps = true;
    })
```

Use case: Tenant information in JWT tokens.

### Header-Based

```csharp
services.AddMultiTenantIsolation()
    .WithHeaderResolutionStrategy(options =>
    {
        options.TenantHeaderName = "X-Tenant-ID";
        options.CaseSensitive = false;
    })
```

Use case: API scenarios with tenant in HTTP headers.

### Path-Based

```csharp
services.AddMultiTenantIsolation()
    .WithPathResolutionStrategy(options =>
    {
        options.TenantPathSegmentIndex = 0; // /tenant1/api/users
        options.IgnoreCase = true;
    })
```

Use case: `yourapp.com/tenant1/dashboard`, `yourapp.com/tenant2/dashboard`

### Custom Strategy

```csharp
services.AddMultiTenantIsolation()
    .WithCustomResolutionStrategy<CustomTenantResolver>(options =>
    {
        // Configure custom resolver
    })
```

Custom resolvers must implement `ITenantResolver`.

## Performance Monitoring

Performance monitoring is mandatory and cannot be disabled.

### Default Configuration

```csharp
services.AddMultiTenantIsolation(); // Monitoring enabled by default

// Default settings:
// - Enabled = true (cannot be disabled)
// - SlowQueryThresholdMs = 1000
// - CollectMetrics = true
// - Uses LoggingMetricsCollector
```

### Custom Options

```csharp
services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.Enabled = true; // Always true
        options.SlowQueryThresholdMs = 500;
        options.CollectMetrics = true; // Always true
        options.LogQueryPlans = false;
    });
```

### Custom Metrics Collectors

Default collector: `LoggingMetricsCollector` (structured logs)

Custom collector registration:

```csharp
services.AddScoped<ITenantMetricsCollector, YourCustomMetricsCollector>();

services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 1000;
    });
```

Integration examples:

```csharp
// OpenTelemetry
services.AddScoped<ITenantMetricsCollector, OpenTelemetryMetricsCollector>();

// Application Insights
services.AddScoped<ITenantMetricsCollector, ApplicationInsightsMetricsCollector>();
```

### Custom Performance Monitor

```csharp
services.AddScoped<ITenantPerformanceMonitor, YourCustomPerformanceMonitor>();

services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 500;
    });
```

Implementation requirements:
- Custom monitors must implement `ITenantPerformanceMonitor`
- Custom collectors must implement `ITenantMetricsCollector`
- Performance monitoring can be configured but never disabled

## Configuration Validation

### Automatic Validation

```csharp
var builder = services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy();

var configuredServices = builder.Build(); // Validation happens here
```

### Explicit Validation

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy()
    .ValidateConfiguration()
    .Build();
```

### Validation Rules

1. At least one tenant resolver must be configured (throws `InvalidOperationException`)
2. A tenant store must be configured (throws `InvalidOperationException`)
3. Tenant cache is optional but recommended (auto-configures in-memory if missing)

## Extension Points

### Custom Service Registration

```csharp
services.AddMultiTenantIsolation()
    .ConfigureServices(services =>
    {
        services.AddScoped<ICustomService, CustomService>();
        services.Configure<CustomOptions>(options => { });
    })
```

### Fluent Service Registration

```csharp
services.AddMultiTenantIsolation()
    .AddService<ICustomService, CustomService>()
    .AddService<IAnotherService>(provider => 
        new AnotherService(provider.GetRequiredService<IDependency>()))
```

## Complete Examples

### Production Web Application

```csharp
services.AddMultiTenantIsolation(options =>
    {
        options.CacheTenantResolution = true;
        options.CacheExpirationMinutes = 30;
    })
    .WithInMemoryTenantCache()
    .WithTenantsStore<EFCoreTenantStore>()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.CacheMappings = true;
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
        options.SystemAdminClaimValue = "SystemAdmin";
    })
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 1000;
        options.CollectMetrics = true;
    })
    .Build();
```

### Microservice with Redis Cache

```csharp
services.AddScoped<ITenantMetricsCollector, ApplicationInsightsMetricsCollector>();

services.AddMultiTenantIsolation()
    .WithTenantDomainCache<RedisTenantCache>(provider =>
        new RedisTenantCache(provider.GetRequiredService<IConnectionMultiplexer>()))
    .WithTenantsStore<HttpTenantStore>(provider =>
        new HttpTenantStore(provider.GetRequiredService<HttpClient>()))
    .WithJwtResolutionStrategy(options =>
    {
        options.TenantClaimType = "tenant_id";
        options.RequireHttps = true;
    })
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 500;
    })
    .Build();
```

## Troubleshooting

### Common Errors

**Error:** `InvalidOperationException: No tenant resolver configured`
**Solution:** Add a resolution strategy using `WithXxxResolutionStrategy()`

**Error:** `InvalidOperationException: No tenant store configured`
**Solution:** Add a tenant store using `WithTenantsStore<T>()`

**Error:** Type registration conflicts
**Solution:** Use validation methods to identify conflicts early

### Performance Issues

- Slow tenant resolution: Enable caching with `CacheTenantResolution = true`
- Cache misses: Adjust `CacheExpirationMinutes` based on data volatility
- Database bottlenecks: Implement custom tenant store with optimized queries

## Best Practices

1. Start with in-memory cache for development
2. Use explicit validation to catch configuration errors early
3. Test resolution strategies with your deployment model
4. Leverage mandatory performance monitoring in production

## Additional Resources

- [Main Library Documentation](../README.md)
- [Features Overview](features.md)
- [Tenant Resolvers Guide](resolvers.md)