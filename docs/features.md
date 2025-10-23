# Features Overview

This library prevents common tenant isolation mistakes through compile-time analysis, runtime validation, and mandatory monitoring, rather than relying on developers to implement isolation correctly.

## Automatic Tenant Context Resolution

Middleware-driven tenant detection resolves tenant context from multiple sources:

- Subdomain resolution: `tenant1.yourapp.com`
- JWT token claims
- HTTP headers: `X-Tenant-ID` or custom
- URL path segments: `/tenant1/api/users`

The middleware handles resolution failures and returns structured error responses.

```csharp
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
    });
```

## Tenant Caching

Basic caching prevents repeated tenant lookups:

- In-memory caching using ASP.NET Core's MemoryCache
- Custom cache implementations for distributed scenarios
- Configurable expiration with time-based invalidation
- Manual cache removal when tenant data changes

Standard caching to avoid repeated tenant store queries.

## Database Isolation Enforcement

Two alternative approaches for tenant isolation:

### Option A: TenantIsolatedDbContext (Automatic)

Global query filters automatically prevent data leaks:

```csharp
public class YourDbContext : TenantIsolatedDbContext
{
    // Automatically applies WHERE TenantId = @currentTenant
    // Validates tenant IDs during SaveChanges
    // Throws exceptions on violations
}

var orders = await context.Orders.ToListAsync(); // Safe - filters applied
```

### Option B: TenantIsolatedRepository (Manual)

Repository pattern with explicit tenant filtering:

```csharp
public class ProjectRepository : TenantIsolatedRepository<Project, YourDbContext>
{
    // Explicit WHERE TenantId = @currentTenant clauses
    // Validates ownership before updates/deletes
    // Auto-assigns tenant ID to new entities
}

var orders = await repository.GetAllAsync(); // Safe - repository filters
```

**When to use each:**
- **TenantIsolatedDbContext**: Simpler, works with existing patterns, uses EF Core global filters
- **TenantIsolatedRepository**: More explicit, works with any DbContext, independent of global filters

Both provide compile-time safety via Roslyn analyzers and runtime validation.

**Key protections:**
- Explicit WHERE clauses (Repository approach)
- Entity ownership validation before modifications
- Throws `TenantIsolationViolationException` on violations
- Logs all operations for auditing

## Mandatory Performance Monitoring

Required monitoring because performance issues often indicate isolation problems.

### Query Performance Tracking
- Slow query logging when exceeding thresholds
- Row count tracking to detect over-fetching
- Tenant filter verification

### Violation Detection
- Exception logging for tenant isolation violations
- User context capture (IP, user agent, auth status)
- Structured logging for monitoring integration

### Cross-Tenant Operation Auditing
- Required justification for isolation bypass operations
- Operation timing tracking
- User attribution for compliance

### Flexible Metrics Collection

Basic logging collector with custom telemetry options:

- Default: `LoggingMetricsCollector` for structured logs
- Custom collectors: Implement `ITenantMetricsCollector`
- Integrations: OpenTelemetry, Application Insights, Prometheus, Datadog

```csharp
// Default setup
services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 1000;
        options.CollectMetrics = true; // Required
    });

// Custom collector
services.AddScoped<ITenantMetricsCollector, YourCustomMetricsCollector>();
```

**Why monitoring is mandatory:**
- Tenant isolation violations are security incidents
- Early detection prevents data leaks from becoming compliance issues
- Performance degradation may indicate bypass attempts
- Audit trails required for forensic analysis

## Roslyn Code Analyzers

Compile-time rules catch violations during build:

### MTI001: Direct DbSet Access Prevention

```csharp
// Compilation error
context.Projects.ToList(); // Direct DbSet access on ITenantIsolated entity

// Compiles correctly
repository.Query().ToList(); // Uses tenant-filtered repository
```

### MTI002: Cross-Tenant Authorization

```csharp
// Compilation error
public async Task DeleteAllUserData() { }

// Compiles with authorization
[AllowCrossTenantAccess("GDPR deletion", "SystemAdmin")]
public async Task DeleteAllUserData() { }
```

### MTI003: Query Filter Verification

Warns about potentially unsafe query patterns.

### MTI005: System Context Authorization

Prevents unauthorized system context creation.

## Safe vs Unsafe Pattern Detection

Analyzers automatically distinguish safe from unsafe usage:

### Safe Patterns
- TenantIsolatedDbContext derivatives (automatic global filters)
- Non-tenant entities only (no isolation needed)
- TenantIsolatedRepository usage (manual validation)
- Authorized cross-tenant operations (explicit permission)

### Unsafe Patterns
- Regular DbContext with ITenantIsolated entities (no protection)
- Direct DbSet access (bypasses filters)
- Unauthorized cross-tenant operations (missing attributes)
- Manual tenant filtering (error-prone)

## Cross-Tenant Operation Management

Controlled access for legitimate system operations:

```csharp
[AllowCrossTenantAccess("Admin reporting", "SystemAdmin")]
public async Task<AdminReport> GenerateSystemReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(
        async () => await GenerateReport(), 
        "Monthly admin report generation"
    );
}
```

Provides:
- Attribute-based authorization
- Required justification logging
- Performance tracking of privileged operations
- Temporary context switching to system mode

## Extension Points

Customization options while maintaining safety:

- Custom tenant resolvers for specific resolution logic
- Custom metrics collectors for monitoring integration
- Custom cache implementations for Redis or distributed caches
- Custom performance monitors for advanced logging

Extensions add functionality but cannot disable safety features or bypass validation.

## Configuration API

Fluent configuration with self-validation:

```csharp
services.AddMultiTenantIsolation(options => {
        options.CacheTenantResolution = true;
        options.CacheExpirationMinutes = 30;
    })
    .WithInMemoryTenantCache()
    .WithTenantsStore<EFCoreTenantStore>()
    .WithSubdomainResolutionStrategy(options => {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
    })
    .WithPerformanceMonitoring(options => {
        options.SlowQueryThresholdMs = 500;
    })
    .ValidateConfiguration();
```

## Common Mistakes Prevented

1. Missing WHERE clauses in tenant-filtered queries
2. Direct DbContext usage bypassing tenant protection
3. Cross-tenant modifications from incorrectly loaded entities
4. Unmonitored queries with poor performance or incorrect data
5. Unaudited privileged operations
6. Configuration differences between environments
7. Silent tenant data leaks

## Design Philosophy

This library is intentionally restrictive:

- Developers forget to implement tenant checks correctly
- Secure by default is better than flexible and dangerous
- Compilation errors catch problems before deployment
- Explicit authorization required for risky operations
- Mandatory monitoring makes problems immediately visible
- Consistent patterns reduce cognitive load and mistakes

The library assumes mistakes will be made and attempts to make them impossible or obvious.

## Additional Resources

- [Main Library Documentation](../README.md)
- [Configuration Guide](configuration.md)
- [Tenant Resolvers Guide](resolvers.md)