# Multi-Tenant Data Isolation Enforcer

[![Build and Test](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Knara.MultiTenant.IsolationEnforcer.svg)](https://www.nuget.org/packages/Knara.MultiTenant.IsolationEnforcer/)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?style=flat&logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Multi-tenant data isolation for .NET applications with compile-time enforcement.

## Problem and Solution

**Problem:** Tenant data leaks are among the most common and costly mistakes in multi-tenant applications.

**Solution:** This library prevents tenant isolation errors through compile-time analysis via Roslyn analyzers and runtime safeguards.

### Before (Unsafe)
```csharp
public async Task<List<Order>> GetOrders()
{
    return await _context.Orders.ToListAsync(); // Returns ALL tenants' data
}
```

### After (Protected)
```csharp
public async Task<List<Order>> GetOrders()
{
    return await _orderRepository.GetAllAsync(); // Automatically tenant-filtered
}

// Attempting direct DbSet access produces:
// Error MTI001: Use ITenantIsolatedRepository<Order> instead of direct DbSet access
```

## Requirements

- .NET 8.0 or later

## Installation

### NuGet Packages
```bash
dotnet add package Knara.MultiTenant.IsolationEnforcer
dotnet add package Knara.MultiTenant.IsolationEnforcer.Analyzers
```

```xml
<ItemGroup>
  <PackageReference Include="Knara.MultiTenant.IsolationEnforcer" Version="1.0.0" />
  <PackageReference Include="Knara.MultiTenant.IsolationEnforcer.Analyzers" Version="1.0.0" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

### Clone and Build
```bash
git clone https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer
cd Knara.MultiTenant.IsolationEnforcer
dotnet build
```

## Quick Start

### 1. Configure Services
```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
    });

app.UseAuthentication();
app.UseMultiTenantIsolation();
```

### 2. Choose Isolation Approach

**Option A: TenantIsolatedDbContext (Automatic Filtering)**
```csharp
public class YourDbContext : TenantIsolatedDbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options, 
        ITenantContextAccessor tenantAccessor, 
        ILogger<YourDbContext> logger) 
        : base(options, tenantAccessor, logger) { }
}

// Use DbContext directly
public async Task<List<Order>> GetOrders()
{
    return await _context.Orders.ToListAsync(); // Automatically filtered
}
```

**Option B: TenantIsolatedRepository (Manual Filtering)**
```csharp
public class OrderService
{
    private readonly TenantIsolatedRepository<Order, YourDbContext> _orderRepo;
    
    public async Task<List<Order>> GetOrders()
    {
        return await _orderRepo.GetAllAsync(); // Manually filtered
    }
}
```

### 3. Mark Entities
```csharp
public class Order : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; } // Auto-assigned
    public string CustomerName { get; set; }
}
```

## Documentation

- [Configuration Guide](docs/configuration.md) - Setup and configuration options
- [Features Overview](docs/features.md) - Library capabilities
- [Tenant Resolvers](docs/resolvers.md) - Tenant detection strategies
- [Demo Project](demo/TaskMasterPro.Api) - Working example showing all library features

## Key Features

### Compile-Time Enforcement
- **MTI001**: Direct DbSet access prohibited
- **MTI002**: Cross-tenant authorization required
- **MTI003**: Filter bypass warnings
- **MTI005**: System context authorization required

### Runtime Protection
- Global query filters: `WHERE TenantId = @currentTenant`
- SaveChanges validation prevents cross-tenant modifications
- Automatic TenantId assignment
- Exception throwing on violations

### Mandatory Monitoring
- Query performance tracking
- Violation logging
- Cross-tenant operation auditing

## Cross-Tenant Operations

For legitimate administrative operations:

```csharp
[AllowCrossTenantAccess("Admin reporting", "SystemAdmin")]
public async Task<AdminReport> GetGlobalReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(
        async () => await GenerateReport(), 
        "Global admin reporting"
    );
}
```

## Tenant Resolution

Built-in strategies:

```csharp
.WithSubdomainResolutionStrategy()  // tenant1.yourapp.com
.WithJwtResolutionStrategy()         // JWT claims
.WithHeaderResolutionStrategy()      // X-Tenant-ID header
.WithPathResolutionStrategy()        // /tenant1/api/users
```

## Comparison to Other Libraries

Compared to libraries like Finbuckle.MultiTenant, this library is more restrictive:

- Compile-time enforcement via Roslyn analyzers
- Mandatory performance monitoring
- Opinionated design with fewer configuration options

This library assumes developers will make mistakes and attempts to prevent them at compile time.

## Migration from Existing Applications

1. Install package and configure services
2. Choose TenantIsolatedDbContext or TenantIsolatedRepository
3. Add ITenantIsolated interface to entities
4. Replace direct DbContext usage with repositories (Option B only)
5. Fix compilation errors guided by analyzers
6. Add database migration for TenantId columns

## When to Use

**Good fit:**
- Teams frequently making tenant isolation mistakes
- Preference for compile-time safety over flexibility
- New multi-tenant applications
- Opinionated tools with fewer configuration options

**Not suitable:**
- Need for maximum flexibility
- Complex tenant resolution requirements beyond built-in resolvers
- Large existing codebases resistant to repository pattern adoption

## License

MIT License. Copyright 2025 Tatyana Asriyan