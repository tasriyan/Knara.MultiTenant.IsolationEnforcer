# 🛡️ Multi-Tenant Data Isolation Enforcer

**Multi-tenant data isolation for .NET applications with compile-time enforcement**

## 🎯 What This Solves

**The Problem**: Your team will accidentally create tenant data leaks. It's not a matter of "if" but "when."

**The Solution**: This library makes it **impossible** to deploy code that violates tenant isolation.

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

### 1. Get the Library

**Option A: Clone and Reference (Recommended)**
```bash
git clone https://github.com/yourusername/multitenant-enforcer.git
cd multitenant-enforcer
dotnet build
```

Then add a project reference to your application:
```xml
<ProjectReference Include="../path/to/MultiTenant.Enforcer/MultiTenant.Enforcer.csproj" />
```

**Option B: Build from Source**
```bash
git clone https://github.com/yourusername/multitenant-enforcer.git
cd multitenant-enforcer
dotnet pack -c Release
```

Then reference the generated .nupkg file in your project.

> **📦 Coming Soon**: NuGet package will be available once the library reaches stable release.

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

## 📚 Documentation

- **[Configuration Guide](configuration.md)** - Complete setup and configuration options
- **[Features Overview](features.md)** - What the library does and why
- **[Tenant Resolvers](resolvers.md)** - How tenant detection works

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

See the [Tenant Resolvers Guide](TenantResolvers.md) for details on each approach.

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

