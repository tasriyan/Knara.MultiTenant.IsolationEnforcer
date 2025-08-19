# 🛡️ Multi-Tenant Data Isolation Enforcer

**Bulletproof multi-tenant data isolation for .NET 8 SaaS applications**


## 🎯 What This Solves

**The Problem**: Your team will accidentally create tenant data leaks. It's not a matter of "if" but "when."

**The Solution**: This enforcer makes it **impossible** to deploy code that violates tenant isolation.

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
// Error MTI001: Use ITenantRepository<Order> instead of direct DbSet access
```

## 🚀 Quick Start

### 1. Install the Package
```bash
dotnet add package MultiTenant.Enforcer
```

### 2. Configure Your Startup (3 lines)
```csharp
// Program.cs
services.AddMultiTenantIsolation<YourDbContext>();

// Pipeline
app.UseAuthentication();
app.UseMultiTenantIsolation(); // Must be after UseAuthentication()
```

### 3. Update Your DbContext
```csharp
// Change this:
public class YourDbContext : DbContext

// To this:
public class YourDbContext : TenantDbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options, 
        ITenantContextAccessor tenantAccessor, 
        ILogger<YourDbContext> logger) 
        : base(options, tenantAccessor, logger) { }
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

### 5. Use Repositories (Enforced by Analyzer)
```csharp
public class OrderController : ControllerBase
{
    private readonly ITenantRepository<Order> _orderRepo; // ✅ Required by analyzer
    
    public async Task<List<Order>> GetOrders()
    {
        return await _orderRepo.GetAllAsync(); // ✅ Automatically tenant-filtered
    }
}
```

## 🛡️ Triple-Layer Protection

### 1. **Compile-Time (Roslyn Analyzers)**
- **MTI001**: Direct DbSet access → Compilation ERROR
- **MTI002**: Missing cross-tenant authorization → Compilation ERROR  
- **MTI003**: Potential filter bypasses → WARNING
- **MTI004**: Entities without repositories → WARNING
- **MTI005**: Unauthorized system context → Compilation ERROR

### 2. **Runtime (EF Core Global Filters)**
- Automatic `WHERE TenantId = @currentTenant` on ALL queries
- SaveChanges validation prevents cross-tenant modifications
- Automatic TenantId assignment on new entities

### 3. **Database (Performance Indexes)**
- Automatic `(TenantId, ...)` indexes on all tenant entities
- Query performance monitoring and optimization

## 📊 What Your Team Cannot Break

| Violation Type | Protection Level | Result |
|---------------|------------------|---------|
| `_context.Orders.ToListAsync()` | **Compile Error** | Won't build |
| Cross-tenant entity modification | **Runtime Exception** | App crashes safely |
| Missing tenant in queries | **Global Filters** | Automatic filtering |
| Performance issues | **Auto Indexes** | Optimized queries |
| Unauthorized cross-tenant ops | **Compile Error** | Won't build |

## 🔧 Advanced Configuration

### JWT Tenant Resolution
```csharp
services.AddMultiTenantIsolation<MyDbContext>(options =>
{
    options.UseJwtTenantResolver(jwt =>
    {
        jwt.TenantIdClaimType = "tenant_id";
        jwt.SystemAdminClaimType = "role";
        jwt.SystemAdminClaimValue = "SystemAdmin";
    });
});
```

### Subdomain Tenant Resolution
```csharp
services.AddMultiTenantIsolation<MyDbContext>(options =>
{
    options.UseSubdomainTenantResolver(subdomain =>
    {
        subdomain.ExcludedSubdomains = new[] { "www", "api", "admin" };
        subdomain.CacheMappings = true;
    });
});
```

### Performance Monitoring
```csharp
services.AddMultiTenantIsolation<MyDbContext>(options =>
{
    options.PerformanceMonitoring.Enabled = true;
    options.PerformanceMonitoring.SlowQueryThresholdMs = 500;
    options.PerformanceMonitoring.LogQueryPlans = true;
});
```

## 🚨 Cross-Tenant Operations (Admin Functions)

For legitimate cross-tenant access (admin reports, user migration):

```csharp
[AllowCrossTenantAccess("Admin needs to view all tenants", "SystemAdmin")]
public async Task<AdminReport> GetGlobalReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
    {
        // This runs in system context - can access all tenants
        var allOrders = await _context.Orders.ToListAsync();
        return GenerateReport(allOrders);
    }, "Global admin reporting");
}
```

## 📈 Performance Features

### Automatic Optimization
- **Tenant-based indexes**: `(TenantId, OtherColumns)` on all entities
- **Query monitoring**: Slow query detection and logging
- **Bulk operations**: Tenant-safe bulk updates and deletes
- **Connection pooling**: Efficient resource usage

### Performance Monitoring
```json
{
  "Level": "Information", 
  "Message": "Query executed: Order.GetOverdue took 45ms, returned 23 rows, tenant: acme-corp",
  "TenantId": "11111111-1111-1111-1111-111111111111",
  "ExecutionTimeMs": 45,
  "RowsReturned": 23
}
```

## 🔍 Visual Studio Experience

### Real-Time Error Detection
```csharp
public async Task<List<Order>> BadMethod()
{
    return await _context.Orders.ToListAsync();
    //           ^^^^^^^^^^^^^^^ 
    //           Red squiggly line with error:
    //           "Use ITenantRepository<Order> instead"
    //           💡 Click for automatic fix
}
```

### Automatic Code Fixes
- **F1 Error**: Direct DbSet access detected
- **Ctrl+.**: Shows "Use ITenantRepository instead" 
- **Enter**: Automatically fixes the code

## 🧪 Testing Support

### Unit Testing
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

### Integration Testing
```csharp
[Test] 
public async Task CrossTenantAccess_Should_Throw_Exception()
{
    // Arrange
    var tenant1Order = CreateOrderForTenant(tenant1Id);
    SetCurrentTenant(tenant2Id);
    
    // Act & Assert
    await Assert.ThrowsAsync<TenantIsolationViolationException>(
        () => _orderRepository.UpdateAsync(tenant1Order));
}
```

## 📋 Migration Guide

### From Existing Applications

1. **Install the package**:
   ```bash
   dotnet add package MultiTenant.Enforcer
   ```

2. **Update your DbContext**:
   ```csharp
   // Before
   public class MyDbContext : DbContext
   
   // After  
   public class MyDbContext : TenantDbContext
   ```

3. **Add the interface to entities**:
   ```csharp
   public class Order : ITenantIsolated
   {
       public Guid TenantId { get; set; } // Add this property
       // ... existing properties
   }
   ```

4. **Fix compilation errors**:
   - Replace `_context.Orders` with `_orderRepository`
   - Inject `ITenantRepository<Order>` instead of `DbContext`

5. **Add migration for TenantId**:
   ```bash
   dotnet ef migrations add AddTenantIdToEntities
   dotnet ef database update
   ```

## 🎛️ Configuration Options

```csharp
services.AddMultiTenantIsolation<MyDbContext>(options =>
{
    // Tenant resolution
    options.UseJwtTenantResolver();
    // or options.UseSubdomainTenantResolver();
    // or options.UseCompositeResolver(typeof(JwtResolver), typeof(SubdomainResolver));
    
    // Security
    options.LogViolations = true;
    options.CacheTenantResolution = true;
    options.CacheExpirationMinutes = 5;
    
    // Performance
    options.PerformanceMonitoring.Enabled = true;
    options.PerformanceMonitoring.SlowQueryThresholdMs = 1000;
    options.PerformanceMonitoring.CollectMetrics = true;
});
```

## 🎭 Example Scenarios

### E-Commerce SaaS
```csharp
// Each store (tenant) sees only their products
public class Product : ITenantIsolated
{
    public Guid TenantId { get; set; } // Store ID
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Automatically filtered by store
var products = await _productRepository.GetAllAsync();
```

### Project Management SaaS  
```csharp
// Each company (tenant) sees only their projects
public class Project : ITenantIsolated  
{
    public Guid TenantId { get; set; } // Company ID
    public string Name { get; set; }
    public List<Task> Tasks { get; set; }
}
```

### Healthcare SaaS
```csharp
// Each clinic (tenant) sees only their patients
public class Patient : ITenantIsolated
{
    public Guid TenantId { get; set; } // Clinic ID
    public string Name { get; set; }
    public List<MedicalRecord> Records { get; set; }
}
```

## 🛠️ Troubleshooting

### Common Issues

**Q: Getting MTI001 errors everywhere**
A: This is expected! The analyzer is protecting you. Replace direct DbSet access with repositories:
```csharp
// ❌ This
_context.Orders.ToListAsync()

// ✅ With this  
_orderRepository.GetAllAsync()
```

**Q: How do I access data across all tenants?**
A: Use the cross-tenant operation manager with proper authorization:
```csharp
[AllowCrossTenantAccess("Admin reporting", "Admin")]
public async Task<Report> GetGlobalReport()
{
    return await _crossTenantManager.ExecuteCrossTenantOperationAsync(
        async () => { /* cross-tenant logic */ }, 
        "Global reporting");
}
```

**Q: Performance is slow**
A: Check that tenant indexes are created. The package auto-creates them, but verify:
```sql
-- Should exist for each tenant entity
CREATE INDEX IX_Orders_TenantId ON Orders(TenantId);
```

## 📚 Documentation

- **[Getting Started Guide](docs/getting-started.md)**
- **[Architecture Overview](docs/architecture.md)**
- **[Migration Guide](docs/migration.md)**
- **[Performance Tuning](docs/performance.md)**
- **[Security Best Practices](docs/security.md)**
- **[API Reference](docs/api-reference.md)**

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for your changes
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **GitHub Issues**: [Report bugs and request features](https://github.com/yourcompany/multitenant-enforcer/issues)
- **Documentation**: [https://docs.multitenant.enforcer/](https://docs.multitenant.enforcer/)
- **Stack Overflow**: Tag your questions with `multitenant-enforcer`

---

**⚡ Made by developers, for developers who are tired of tenant data leaks.**

*"The best security bug is the one that can't be introduced in the first place."*
