# Multi-Tenant Isolation Features

This opinionated multi-tenant isolation library is designed to prevent the common tenant isolation mistakes that development teams repeatedly make. Instead of relying on developers to "remember to be careful," the library enforces isolation through compile-time analysis, runtime validation, and mandatory monitoring.

## 🛡️ Automatic Tenant Context Resolution

**Middleware-driven tenant detection** that resolves tenant context from multiple sources:

- **Subdomain resolution**: `tenant1.yourapp.com` → Tenant ID
- **JWT token claims**: Extract tenant from authentication tokens  
- **HTTP headers**: `X-Tenant-ID` or custom headers
- **URL path segments**: `/tenant1/api/users` → Tenant ID
- **Composite strategies**: Try multiple resolution methods with fallback

The middleware catches resolution failures and returns structured error responses. This eliminates the "forgot to check the tenant" problem by making tenant context available automatically.

```csharp
// Sets tenant context for all downstream operations
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
    });
```

## 🗃️ Tenant Caching

**Basic caching** that prevents repeated tenant lookups:

- **In-memory caching** using ASP.NET Core's MemoryCache
- **Custom cache implementations** for distributed scenarios  
- **Configurable expiration** with simple time-based invalidation
- **Manual cache removal** when tenant data changes

Nothing fancy here - just standard caching to avoid hitting the tenant store repeatedly for the same request or user session.

## 🔒 Database Isolation Enforcement

### TenantIsolatedDbContext
**Automatic query filtering** that prevents tenant data leaks:

```csharp
public class YourDbContext : TenantIsolatedDbContext
{
    // Applies global query filters to ITenantIsolated entities
    // Validates tenant IDs during SaveChanges
    // Throws exceptions on cross-tenant violations
}
```

**What it prevents:**
- Queries returning data from the wrong tenant
- Saving entities with incorrect tenant IDs
- Modifying or deleting entities from other tenants
- Accidental system context usage

### TenantIsolatedRepository
**Repository pattern** that adds manual tenant filtering:

```csharp
public class ProjectRepository : TenantIsolatedRepository<Project, YourDbContext>
{
    // Queries include explicit tenant filtering
    // Validates ownership before updates/deletes
    // Auto-assigns tenant ID to new entities
}
```

**Key protections:**
- Doesn't rely solely on global filters - adds explicit WHERE clauses
- Checks entity ownership before allowing modifications
- Throws `TenantIsolationViolationException` when violations are detected
- Logs all operations for audit purposes

## 🚨 Mandatory Performance Monitoring

**Required monitoring** because performance problems often indicate tenant isolation issues:

### Query Performance Tracking
- **Slow query logging** when queries exceed configurable thresholds
- **Row count tracking** to catch queries returning too much data
- **Tenant filter verification** to confirm filters are applied

### Violation Detection
- **Exception logging** for tenant isolation violations
- **User context capture** including IP address, user agent, authentication status
- **Structured logging** for integration with monitoring systems

### Cross-Tenant Operation Auditing
- **Required justification** for operations that bypass tenant isolation
- **Operation timing** to track privileged operation performance
- **User attribution** for compliance and debugging

```csharp
// Monitoring cannot be disabled - it's built into the library
services.AddMultiTenantIsolation()
    .WithPerformanceMonitoring(options =>
    {
        options.SlowQueryThresholdMs = 1000;
        options.CollectMetrics = true; // Always required
    });
```

## 🔧 Roslyn Code Analyzers

**Compile-time rules** that catch violations during build:

### MTI001: Direct DbSet Access Prevention
Stops developers from bypassing tenant isolation:
```csharp
// ❌ Compilation error
context.Projects.ToList(); // Direct DbSet access on ITenantIsolated entity

// ✅ Compiles correctly  
repository.Query().ToList(); // Uses tenant-filtered repository
```

### MTI002: Cross-Tenant Authorization
Requires explicit authorization for dangerous operations:
```csharp
// ❌ Compilation error without attribute
public async Task DeleteAllUserData() { /* cross-tenant logic */ }

// ✅ Compiles with authorization
[AllowCrossTenantAccess("GDPR deletion", "SystemAdmin")]
public async Task DeleteAllUserData() { /* authorized logic */ }
```

### MTI003: Query Filter Verification
Warns about potentially unsafe query patterns.

### MTI005: System Context Authorization
Prevents unauthorized creation of system contexts.

## 🎯 Safe vs Unsafe Pattern Detection

The analyzers distinguish between safe and unsafe usage automatically:

### ✅ SAFE Patterns
- **TenantIsolatedDbContext derivatives**: Global filters applied automatically
- **Non-tenant entities only**: No tenant isolation needed
- **TenantIsolatedRepository usage**: Manual validation included  
- **Authorized cross-tenant operations**: Explicit permission granted

### ❌ UNSAFE Patterns  
- **Regular DbContext with ITenantIsolated entities**: No automatic protection
- **Direct DbSet access**: Bypasses all filters
- **Unauthorized cross-tenant operations**: Missing required attributes
- **Manual tenant filtering**: Easy to forget or implement incorrectly

## 🌐 Cross-Tenant Operation Management

**Controlled access** for legitimate system operations:

```csharp
[AllowCrossTenantAccess("Admin reporting", "SystemAdmin")]
public async Task<AdminReport> GenerateSystemReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(
        async () => {
            // System context allows cross-tenant data access
            return await GenerateReport();
        }, 
        "Monthly admin report generation"
    );
}
```

**What it provides:**
- **Attribute-based authorization** for dangerous operations
- **Required justification** logged for every operation
- **Performance tracking** of privileged operations  
- **Temporary context switching** to system mode

## 🧩 Extension Points

**Customization options** while maintaining safety:

- **Custom tenant resolvers** for specific resolution logic
- **Custom metrics collectors** for integration with existing monitoring
- **Custom cache implementations** for Redis or other distributed caches
- **Custom performance monitors** for advanced logging needs

Extensions can add functionality but cannot disable safety features or bypass validation.

## 🎪 Configuration API

**Fluent configuration** that validates itself:

```csharp
services.AddMultiTenantIsolation(options => {
        options.CacheTenantResolution = true;
        options.CacheExpirationMinutes = 30;
    })
    .WithInMemoryTenantCache()
    .WithTenantsStore<EFCoreTenantStore>() 
    .WithSubdomainResolutionStrategy(options => {
        options.ExcludedSubdomains = ["www", "api", "admin"];
    })
    .WithPerformanceMonitoring(options => {
        options.SlowQueryThresholdMs = 500;
    })
    .ValidateConfiguration(); // Throws exceptions if misconfigured
```

## 🚫 What This Library Prevents

**Common mistakes that happen repeatedly:**

1. **Missing WHERE clauses** in tenant-filtered queries
2. **Direct DbContext usage** that bypasses tenant protection  
3. **Cross-tenant modifications** from incorrectly loaded entities
4. **Unmonitored queries** that perform poorly or return wrong data
5. **Unaudited privileged operations** that can't be traced later
6. **Configuration mistakes** between development and production
7. **Silent tenant data leaks** that go unnoticed

## 🎯 Design Philosophy

This library is **intentionally limiting** because:

- **Developers forget** to implement tenant checks correctly
- **Secure by default** is better than flexible and dangerous
- **Compilation errors** catch problems before deployment  
- **Explicit authorization** is required for risky operations
- **Mandatory monitoring** makes problems visible immediately
- **Consistent patterns** reduce cognitive load and mistakes

The assumption is that if you can make a tenant isolation mistake, you probably will. The library tries to make those mistakes impossible or at least very obvious.
