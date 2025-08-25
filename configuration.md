# Multi-Tenant Isolation Configuration Guide

This guide covers all configuration options for the Multi-Tenant Isolation library. The library is **opinionated by design** to prevent critical tenant isolation errors commonly made by development teams.

## Quick Start

The simplest configuration requires only a tenant store and resolution strategy:

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy();
```

## Core Configuration

### 1. Initial Setup

```csharp
services.AddMultiTenantIsolation(options =>
{
    options.CacheTenantResolution = true;
    options.CacheExpirationMinutes = 30;
    // Additional MultiTenantOptions configuration
});
```

**Note**: If no configuration is provided, default options are automatically applied.

## Tenant Cache Configuration

### In-Memory Cache (Recommended)

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    // ... other configuration
```

**Benefits**: Fast, simple, works out-of-the-box for most scenarios.

### Custom Cache Implementation

```csharp
// Simple registration
services.AddMultiTenantIsolation()
    .WithTenantDomainCache<RedisTenantCache>()
    // ... other configuration

// Factory-based registration (for complex dependencies)
services.AddMultiTenantIsolation()
    .WithTenantDomainCache<RedisTenantCache>(provider => 
        new RedisTenantCache(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            provider.GetRequiredService<ILogger<RedisTenantCache>>()
        ))
    // ... other configuration
```

**Use Cases**: Distributed caching, Redis, custom cache implementations.

## Tenant Store Configuration

### Simple Registration

```csharp
services.AddMultiTenantIsolation()
    .WithTenantsStore<EFCoreTenantStore>()
    // ... other configuration
```

### Factory-Based Registration

```csharp
services.AddMultiTenantIsolation()
    .WithTenantsStore<EFCoreTenantStore>(provider =>
        new EFCoreTenantStore(
            provider.GetRequiredService<MyDbContext>(),
            provider.GetRequiredService<ILogger<EFCoreTenantStore>>()
        ))
    // ... other configuration
```

**Requirements**: Your tenant store must implement `ITenantStore`. This is **mandatory** - the library will throw an exception if no tenant store is configured.

## Tenant Resolution Strategies

### Subdomain-Based Resolution

```csharp
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.CacheMappings = true;
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
        options.SystemAdminClaimValue = "SystemAdmin";
    })
    // ... other configuration
```

**Use Case**: `tenant1.yourapp.com`, `tenant2.yourapp.com`

### JWT-Based Resolution

```csharp
services.AddMultiTenantIsolation()
    .WithJwtResolutionStrategy(options =>
    {
        options.TenantClaimType = "tenant_id";
        options.RequireHttps = true;
        // Additional JWT-specific options
    })
    // ... other configuration
```

**Use Case**: Tenant information embedded in JWT tokens.

### Header-Based Resolution

```csharp
services.AddMultiTenantIsolation()
    .WithHeaderResolutionStrategy(options =>
    {
        options.TenantHeaderName = "X-Tenant-ID";
        options.CaseSensitive = false;
        // Additional header-specific options
    })
    // ... other configuration
```

**Use Case**: API scenarios where tenant is specified via HTTP headers.

### Path-Based Resolution

```csharp
services.AddMultiTenantIsolation()
    .WithPathResolutionStrategy(options =>
    {
        options.TenantPathSegmentIndex = 0; // /tenant1/api/users
        options.IgnoreCase = true;
        // Additional path-specific options
    })
    // ... other configuration
```

**Use Case**: `yourapp.com/tenant1/dashboard`, `yourapp.com/tenant2/dashboard`

### Custom Resolution Strategy

```csharp
services.AddMultiTenantIsolation()
    .WithCustomResolutionStrategy<CustomTenantResolver>(options =>
    {
        // Configure your custom resolver options
    })
    // ... other configuration
```

**Requirements**: Your resolver must implement `ITenantResolver`.

## Performance Monitoring Configuration

> **⚠️ IMPORTANT**: Performance monitoring is **MANDATORY** and cannot be disabled. This is an opinionated design choice to prevent performance-related tenant isolation issues.

### Basic Performance Monitoring

```csharp
services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.Enabled = true; // Always true, cannot be disabled
        options.SlowQueryThresholdMs = 1000;
        options.CollectMetrics = true;
    })
    // ... other configuration
```

### Custom Metrics Collector

```csharp
// Simple registration
services.AddMultiTenantIsolation()
    .WithCustomMetricsCollector<PrometheusMetricsCollector>()
    // ... other configuration

// Factory-based registration
services.AddMultiTenantIsolation()
    .WithCustomMetricsCollector<PrometheusMetricsCollector>(provider =>
        new PrometheusMetricsCollector(
            provider.GetRequiredService<IMetricServer>()
        ))
    // ... other configuration
```

### Custom Performance Monitor

```csharp
services.AddMultiTenantIsolation()
    .WithCustomPerformanceMonitor<CustomPerformanceMonitor>()
    // ... other configuration
```

### Complete Custom Performance Monitoring

```csharp
services.AddMultiTenantIsolation()
    .WithCustomPerformanceMonitoring<CustomPerformanceMonitor, CustomMetricsCollector>(options =>
    {
        options.SlowQueryThresholdMs = 500;
        options.CollectMetrics = true;
    })
    // ... other configuration
```

**Requirements**: 
- Custom monitors must implement `ITenantPerformanceMonitor`
- Custom collectors must implement `ITenantMetricsCollector`
- Security monitoring (violations, audit trails) must still be implemented

## Configuration Validation

### Automatic Validation

```csharp
var builder = services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy();

// Validation happens automatically when Build() is called
var configuredServices = builder.Build();
```

### Explicit Validation

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy()
    .ValidateConfiguration() // Explicit validation
    .Build();
```

### Validation Rules

The library enforces these validation rules:

1. **At least one tenant resolver must be configured** - Will throw `InvalidOperationException` if missing
2. **A tenant store must be configured** - Will throw `InvalidOperationException` if missing  
3. **Tenant cache is optional but recommended** - Auto-configures in-memory cache if none specified

## Extension Points

### Custom Service Registration

```csharp
services.AddMultiTenantIsolation()
    .ConfigureServices(services =>
    {
        services.AddScoped<ICustomService, CustomService>();
        services.Configure<CustomOptions>(options => { /* ... */ });
    })
    // ... other configuration
```

### Fluent Service Registration

```csharp
services.AddMultiTenantIsolation()
    .AddService<ICustomService, CustomService>()
    .AddService<IAnotherService>(provider => 
        new AnotherService(provider.GetRequiredService<IDependency>()))
    // ... other configuration
```

## Complete Configuration Examples

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

### API with Multiple Resolution Strategies

```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<ApiTenantStore>()
    .WithCompositeResolutionStrategy(
        typeof(JwtTenantResolver),
        typeof(HeaderTenantResolver),
        typeof(SubdomainTenantResolver)
    )
    .WithCustomMetricsCollector<PrometheusMetricsCollector>()
    .Build();
```

### Microservice with Redis Cache

```csharp
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
        options.SlowQueryThresholdMs = 500; // Stricter for microservices
    })
    .Build();
```

## Best Practices

### Development Workflow

1. **Start with in-memory cache** - Simple and effective for development
2. **Use explicit validation** - Catch configuration errors early
3. **Test resolution strategies** - Ensure your chosen strategy works with your deployment model
4. **Monitor in production** - Leverage the mandatory performance monitoring

## Troubleshooting

### Common Configuration Errors

**Error**: `InvalidOperationException: No tenant resolver configured`
**Solution**: Add at least one resolution strategy using `WithXxxResolutionStrategy()`

**Error**: `InvalidOperationException: No tenant store configured`  
**Solution**: Add a tenant store using `WithTenantsStore<T>()`

**Error**: Type registration conflicts
**Solution**: Use the validation methods to identify conflicts early in the pipeline

### Performance Issues

- **Slow tenant resolution**: Enable caching with `CacheTenantResolution = true`
- **Cache misses**: Adjust `CacheExpirationMinutes` based on your tenant data volatility
- **Database bottlenecks**: Implement a custom tenant store with optimized queries

Remember: This library is intentionally opinionated to prevent tenant isolation errors. Embrace the constraints—they're designed to keep your multi-tenant application secure and performant.
