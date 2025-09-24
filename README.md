# 🛡️ Multi-Tenant Data Isolation Enforcer

[![Build and Test](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/build.yml)
[![Publish NuGet Packages](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/publish.yml/badge.svg)](https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer/actions/workflows/publish.yml)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?style=flat&logo=dotnet&logoColor=white)

**Multi-tenant data isolation for .NET applications with compile-time enforcement**

## 🎯 What This Solves

***The Problem:*** Tenant data leaks are one of the most common and costly mistakes in multi-tenant applications.

***The Solution:*** This library catches the most common tenant isolation mistakes before they reach production, through compile-time analysis and runtime safeguards.

### ❌ Before (Dangerous)
```csharp
// This compiles but leaks data across tenants
public async Task<List<Order>> GetOrders()
{
    return await _context.Orders.ToListAsync(); // 💀 Returns ALL tenants' data
}
```

### ✅ After (Protected)
```csharp
// This is the only way your team CAN write code
public async Task<List<Order>> GetOrders()
{
    return await _orderRepository.GetAllAsync(); // 🛡️ Automatically tenant-filtered
}

// Trying the old way gives COMPILE ERROR:
// Error MTI001: Use ITenantIsolatedRepository<Order> instead of direct DbSet access
```

## 🚀 Quick Start

> **⚠️ Requirements**: .NET 8.0 or later

### 1. Get the Library

**Option A: NuGet Packages**
```bash

dotnet add package Knara.MultiTenant.IsolationEnforcer
dotnet add package Knara.MultiTenant.IsolationEnforcer.Analyzers

```

Then reference the generated .nupkg files in your project.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Main library -->
    <PackageReference Include="Knara.MultiTenant.IsolationEnforcer" Version="1.0.0" />
    
    <!-- Compile-time safety analyzers -->
    <PackageReference Include="Knara.MultiTenant.IsolationEnforcer.Analyzers" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" >
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Option B: Clone and Reference**
```bash

git clone https://github.com/tasriyan/Knara.MultiTenant.IsolationEnforcer
cd Knara.MultiTenant.IsolationEnforcer
dotnet build

cd Knara.MultiTenant.IsolationEnforcer.Analyzers
dotnet build

```

Then add project references to your application:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework> <!-- or net9.0 -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Main library reference -->
    <ProjectReference Include="../path/to/Knara.MultiTenant.IsolationEnforcer/Knara.MultiTenant.IsolationEnforcer.csproj" />
    
    <!-- Compile-time safety analyzers (highly recommended) -->
    <ProjectReference Include="../path/to/Knara.MultiTenant.IsolationEnforcer.Analyzers/Knara.MultiTenant.IsolationEnforcer.Analyzers.csproj" 
                      OutputItemType="Analyzer" 
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

### 2. Configure Services
```csharp
services.AddMultiTenantIsolation()
    .WithInMemoryTenantCache()
    .WithTenantsStore<YourTenantStore>()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
    });

// Add middleware
app.UseAuthentication();
app.UseMultiTenantIsolation(); 
```

### 3. Choose Your Isolation Approach

You have two options for tenant isolation:

**Option A: Use TenantIsolatedDbContext (Automatic)**
```csharp
// Change this:
public class YourDbContext : DbContext

// To this:
public class YourDbContext : TenantIsolatedDbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options, 
        ITenantContextAccessor tenantAccessor, 
        ILogger<YourDbContext> logger) 
        : base(options, tenantAccessor, logger) { }
}

// Then use DbContext directly - automatic tenant filtering
public class OrderService
{
    private readonly YourDbContext _context;
    
    public async Task<List<Order>> GetOrders()
    {
        return await _context.Orders.ToListAsync(); // ✅ Automatically tenant-filtered
    }
}
```

**Option B: Use TenantIsolatedRepository (Manual)**
```csharp
// Keep your existing DbContext, use repositories for safety
public class OrderService
{
    private readonly TenantIsolatedRepository<Order, YourDbContext> _orderRepo;
    
    public async Task<List<Order>> GetOrders()
    {
        return await _orderRepo.GetAllAsync(); // ✅ Manually tenant-filtered
    }
}
```

### 4. Mark Your Entities
```csharp
public class Order : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; } // Required - auto-assigned
    public string CustomerName { get; set; }
    // ... other properties
}
```

### 5. Use Repositories (Enforced by Roslyn Analyzers)
```csharp
public class OrderService
{
    private readonly TenantIsolatedRepository<Order, YourDbContext> _orderRepo;
    
    public async Task<List<Order>> GetOrders()
    {
        return await _orderRepo.GetAllAsync(); // ✅ Automatically tenant-filtered
    }
}
```

## 📚 Documentation

- **[Configuration Guide](configuration.md)** - Complete setup and configuration options
- **[Features Overview](features.md)** - What the library does and why
- **[Tenant Resolvers](resolvers.md)** - How tenant detection works

## 🛡️ What Makes This Different

**Compared to other multi-tenant libraries** (like Finbuckle.MultiTenant), this one is more paranoid:

- **Compile-time enforcement**: Roslyn analyzers prevent unsafe code from building
- **Mandatory monitoring**: Performance tracking is required, not optional
- **Opinionated approach**: Fewer choices, more guardrails

Most multi-tenant libraries give you flexible tools assuming competent developers. Mine assumes they'll screw up and tries to make that impossible.


## 📋 Protection Layers

### 1. Compile-Time (Roslyn Analyzers)
- **MTI001**: Direct DbSet access → Compilation ERROR
- **MTI002**: Missing cross-tenant authorization → Compilation ERROR  
- **MTI003**: Potential filter bypasses → WARNING
- **MTI005**: Unauthorized system context → Compilation ERROR

### 2. Runtime Protection
- Global query filters: `WHERE TenantId = @currentTenant` on all queries
- SaveChanges validation prevents cross-tenant modifications
- Automatic TenantId assignment for new entities
- Exception throwing on tenant violations

### 3. Performance Monitoring
- Mandatory query performance tracking
- Tenant isolation violation logging
- Cross-tenant operation auditing

## 🚨 Cross-Tenant Operations (Admin Functions)

For legitimate cross-tenant access:

```csharp
[AllowCrossTenantAccess("Admin needs to view all tenants", "SystemAdmin")]
public async Task<AdminReport> GetGlobalReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
    {
        // This runs in system context - can access all tenants
        return await GenerateReport();
    }, "Global admin reporting");
}
```

## 🔧 Tenant Resolution Strategies

The library includes several built-in ways to determine which tenant a request belongs to:

```csharp
// Subdomain: tenant1.yourapp.com → tenant1
.WithSubdomainResolutionStrategy()

// JWT claims: Extract from authentication token  
.WithJwtResolutionStrategy()

// HTTP headers: X-Tenant-ID header
.WithHeaderResolutionStrategy()

// URL path: /tenant1/api/users → tenant1
.WithPathResolutionStrategy()
```

See the [Tenant Resolvers Guide](resolvers.md) for details on each approach.

## 🧪 Testing Support

```csharp
[Test]
public async Task Repository_Should_Filter_By_Tenant()
{
    // Arrange
    var tenantId = Guid.NewGuid();
    _tenantAccessor.SetContext(TenantContext.ForTenant(tenantId, "Test"));
    
    // Act
    var orders = await _orderRepository.GetAllAsync();
    
    // Assert
    orders.Should().AllSatisfy(o => o.TenantId.Should().Be(tenantId));
}
```

## 🛠️ Migration from Existing Apps

1. **Install package and configure services**
2. **Choose your approach**: TenantIsolatedDbContext OR TenantIsolatedRepository
3. **Add ITenantIsolated interface** to tenant entities
4. **Replace direct DbContext usage** with repositories (if using Option B)
5. **Fix compilation errors** (the analyzers will guide you)
6. **Add database migration** for TenantId columns

The Roslyn analyzers will catch most issues during the migration process.

## 🎯 When to Use This

**Good fit:**
- Your team keeps making tenant isolation mistakes
- You want compile-time safety over flexibility
- You're building a new multi-tenant application
- You prefer opinionated tools with fewer configuration options

**Not a good fit:**  
- You need maximum flexibility in your multi-tenant approach
- You have complex tenant resolution requirements that don't fit the built-in resolvers
- You're working with a large existing codebase that can't easily adopt the repository pattern

## 📄 License

MIT License - see the [LICENSE](LICENSE) file for details.
