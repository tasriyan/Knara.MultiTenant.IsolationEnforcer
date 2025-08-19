# Multi-Tenant Violation Examples and Testing Scenarios

## 1. Compilation Errors Your Team Will See

### Example 1: Direct DbSet Access (MTI001)
```csharp
// TaskMasterPro.Api/Controllers/BAD_ProjectsController.cs
// THIS WILL NOT COMPILE with our analyzer
[ApiController]
[Route("api/bad-projects")]
public class BadProjectsController : ControllerBase
{
    private readonly TaskMasterDbContext _context;

    public BadProjectsController(TaskMasterDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<List<Project>> GetProjects()
    {
        // ❌ COMPILATION ERROR MTI001: 
        // "Direct DbSet access on tenant-isolated entity"
        // "Use ITenantRepository<Project> instead of direct DbSet access to ensure tenant isolation"
        return await _context.Projects.ToListAsync();
        //           ^^^^^^^^^^^^^^^^
        //           Red squiggly line appears here in Visual Studio
    }

    [HttpGet("search")]
    public async Task<List<Project>> SearchProjects(string term)
    {
        // ❌ COMPILATION ERROR MTI001: Even with filtering, direct access is blocked
        return await _context.Set<Project>()
            .Where(p => p.Name.Contains(term))
            .ToListAsync();
    }
}
```

**Visual Studio Error Message:**
```
Error MTI001: Direct DbSet access on tenant-isolated entity
Use ITenantRepository<Project> instead of direct DbSet access to ensure tenant isolation
```

### Example 2: Missing Cross-Tenant Authorization (MTI002)
```csharp
// TaskMasterPro.Api/Controllers/BAD_AdminController.cs
[ApiController]
[Route("api/bad-admin")]
public class BadAdminController : ControllerBase
{
    private readonly ICrossTenantOperationManager _crossTenantManager;

    [HttpGet("companies")]
    // ❌ COMPILATION ERROR MTI002:
    // "Cross-tenant operation without authorization"
    // "Method using ICrossTenantOperationManager must have [AllowCrossTenantAccess] attribute"
    public async Task<ActionResult> GetAllCompanies()
    {
        return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
        {
            // This method uses cross-tenant operations but lacks authorization
            return Ok("data");
        }, "Unauthorized access attempt");
    }
}
```

**Visual Studio Error Message:**
```
Error MTI002: Cross-tenant operation without authorization
Method using ICrossTenantOperationManager must have [AllowCrossTenantAccess] attribute
```

### Example 3: Code Fix Provider in Action
```csharp
// Before (triggers error and code fix):
public async Task<List<Project>> GetProjects()
{
    return await _context.Projects.ToListAsync(); // ❌ Error with "Quick Fix" option
}

// After applying code fix (automatically generated):
public async Task<List<Project>> GetProjects()
{
    return await _projectRepository.GetAllAsync(); // ✅ Fixed
}
```

## 2. Runtime Violations and Protection

### Example 1: Cross-Tenant Data Access Attempt
```csharp
// TaskMasterPro.Tests/Runtime/TenantViolationTests.cs
[Fact]
public async Task AttemptCrossTenantAccess_ShouldThrowException()
{
    // Arrange
    var tenant1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var tenant2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    
    // Create project for tenant2
    var tenant2Project = new Project
    {
        Id = Guid.NewGuid(),
        TenantId = tenant2Id,
        Name = "Tenant2 Secret Project",
        CreatedAt = DateTime.UtcNow
    };
    
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    
    // Set tenant context to tenant1
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    tenantAccessor.SetContext(TenantContext.ForTenant(tenant1Id, "Test"));
    
    context.Projects.Add(tenant2Project);
    
    // Act & Assert
    var exception = await Assert.ThrowsAsync<TenantIsolationViolationException>(
        () => context.SaveChangesAsync());
        
    exception.Message.Should().Contain("attempted to add Project with TenantId");
    exception.Message.Should().Contain(tenant2Id.ToString());
    exception.Message.Should().Contain(tenant1Id.ToString());
}
```

### Example 2: Automatic Tenant ID Assignment
```csharp
[Fact]
public async Task CreateProject_WithoutTenantId_ShouldAutoAssign()
{
    // Arrange
    var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    
    tenantAccessor.SetContext(TenantContext.ForTenant(tenantId, "Test"));
    
    var project = new Project
    {
        Id = Guid.NewGuid(),
        // TenantId is intentionally NOT set
        Name = "Auto-assigned Tenant Project",
        CreatedAt = DateTime.UtcNow
    };
    
    // Act
    context.Projects.Add(project);
    await context.SaveChangesAsync();
    
    // Assert
    project.TenantId.Should().Be(tenantId); // Automatically assigned!
}
```

### Example 3: Global Query Filter Protection
```csharp
[Fact]
public async Task QueryWithoutTenantFilter_ShouldStillBeTenantIsolated()
{
    // Arrange
    await SeedProjectsForMultipleTenants();
    var tenant1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    
    tenantAccessor.SetContext(TenantContext.ForTenant(tenant1Id, "Test"));
    
    // Act - Even though we don't explicitly filter by tenant, EF global filters apply
    var projects = await context.Projects
        .Where(p => p.Name.Contains("Project"))
        .ToListAsync();
    
    // Assert
    projects.Should().NotBeEmpty();
    projects.Should().AllSatisfy(p => p.TenantId.Should().Be(tenant1Id));
}
```

## 3. Performance Testing Scenarios

### Load Test with Large Tenant Dataset
```csharp
// TaskMasterPro.Tests/Performance/TenantPerformanceTests.cs
[Fact]
public async Task QueryLargeTenantDataset_ShouldPerformWell()
{
    // Arrange - Create large dataset
    var tenantId = Guid.NewGuid();
    await GenerateLargeDataset(tenantId, projectCount: 1000, tasksPerProject: 100);
    
    using var scope = _factory.Services.CreateScope();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    var taskRepository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    
    tenantAccessor.SetContext(TenantContext.ForTenant(tenantId, "Performance Test"));
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    var overdueTasks = await taskRepository.GetOverdueTasksAsync();
    stopwatch.Stop();
    
    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500); // Should complete in under 500ms
    overdueTasks.Should().AllSatisfy(t => t.TenantId.Should().Be(tenantId));
    
    // Verify query plan used tenant index
    // This would be logged by our performance monitoring
}

[Fact]
public async Task BulkOperations_ShouldRespectTenantIsolation()
{
    // Arrange
    var tenant1Id = Guid.NewGuid();
    var tenant2Id = Guid.NewGuid();
    
    await SeedTasksForTenants(tenant1Id, tenant2Id, tasksPerTenant: 500);
    
    using var scope = _factory.Services.CreateScope();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    var taskRepository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    
    tenantAccessor.SetContext(TenantContext.ForTenant(tenant1Id, "Bulk Test"));
    
    // Act - Bulk update all tasks to "In Progress"
    await taskRepository.BulkUpdateAsync(
        filter: t => t.Status == TaskStatus.ToDo,
        updateExpression: t => t.SetProperty(x => x.Status, TaskStatus.InProgress)
    );
    
    // Assert - Verify only tenant1 tasks were updated
    var tenant1Tasks = await GetAllTasksForTenant(tenant1Id);
    var tenant2Tasks = await GetAllTasksForTenant(tenant2Id);
    
    tenant1Tasks.Where(t => t.Status == TaskStatus.InProgress).Should().HaveCountGreaterThan(0);
    tenant2Tasks.Where(t => t.Status == TaskStatus.ToDo).Should().HaveCountGreaterThan(0); // Unchanged
}
```

## 4. Cross-Tenant Scenarios Testing

### Legitimate Admin Operations
```csharp
[Fact]
public async Task SystemAdmin_CanAccessAllTenantData()
{
    // Arrange
    await SeedMultipleTenants();
    
    using var scope = _factory.Services.CreateScope();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    var crossTenantManager = scope.ServiceProvider.GetRequiredService<ICrossTenantOperationManager>();
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    
    // Set system context
    tenantAccessor.SetContext(TenantContext.SystemContext("Integration Test"));
    
    // Act - Admin operation to get statistics across all tenants
    var statistics = await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
    {
        return await context.Projects
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, ProjectCount = g.Count() })
            .ToListAsync();
    }, "Admin getting tenant statistics");
    
    // Assert
    statistics.Should().HaveCountGreaterThan(1); // Multiple tenants
    statistics.Sum(s => s.ProjectCount).Should().BeGreaterThan(0);
}

[Fact]
public async Task UserMigration_ShouldWorkCorrectly()
{
    // Arrange
    var fromTenantId = Guid.NewGuid();
    var toTenantId = Guid.NewGuid();
    
    var user = await CreateUserInTenant(fromTenantId, "test@example.com");
    await CreateProjectsForUser(user.Id, fromTenantId, 3);
    
    using var scope = _factory.Services.CreateScope();
    var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
    var crossTenantManager = scope.ServiceProvider.GetRequiredService<ICrossTenantOperationManager>();
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    
    tenantAccessor.SetContext(TenantContext.SystemContext("User Migration Test"));
    
    // Act - Migrate user between tenants
    await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
    {
        var userToMigrate = await context.Users.FirstAsync(u => u.Id == user.Id);
        userToMigrate.TenantId = toTenantId;
        
        // Also migrate user's projects
        var userProjects = await context.Projects
            .Where(p => p.ProjectManagerId == user.Id)
            .ToListAsync();
            
        foreach (var project in userProjects)
        {
            project.TenantId = toTenantId;
        }
        
        await context.SaveChangesAsync();
        return true;
    }, "User migration between tenants");
    
    // Assert - Verify migration
    var migratedUser = await GetUserById(user.Id);
    migratedUser.TenantId.Should().Be(toTenantId);
    
    var migratedProjects = await GetProjectsByManager(user.Id);
    migratedProjects.Should().AllSatisfy(p => p.TenantId.Should().Be(toTenantId));
}
```

## 5. Integration Test with HTTP Clients

### Subdomain-Based Tenant Resolution
```csharp
[Fact]
public async Task DifferentSubdomains_ShouldAccessDifferentTenantData()
{
    // Arrange
    var acmeClient = _factory.CreateClientWithHost("acme.localhost");
    var techClient = _factory.CreateClientWithHost("techinnovations.localhost");
    
    // Act
    var acmeProjects = await acmeClient.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
    var techProjects = await techClient.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
    
    // Assert
    acmeProjects.Should().NotBeNull();
    techProjects.Should().NotBeNull();
    
    // Verify different tenant data
    var acmeTenantId = acmeProjects.First().TenantId;
    var techTenantId = techProjects.First().TenantId;
    
    acmeTenantId.Should().NotBe(techTenantId);
    acmeProjects.Should().AllSatisfy(p => p.TenantId.Should().Be(acmeTenantId));
    techProjects.Should().AllSatisfy(p => p.TenantId.Should().Be(techTenantId));
}

[Fact]
public async Task CrossTenantRequest_WithoutAuthorization_ShouldReturn403()
{
    // Arrange
    var client = _factory.CreateClientWithHost("acme.localhost");
    
    // Act - Try to access admin endpoint without system admin role
    var response = await client.GetAsync("/api/admin/companies");
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

## 6. Logging and Monitoring Tests

### Violation Logging
```csharp
[Fact]
public async Task TenantViolation_ShouldBeLogged()
{
    // Arrange
    var loggerFactory = new TestLoggerFactory();
    var logger = loggerFactory.CreateLogger<TenantDbContext>();
    
    // ... setup context with test logger
    
    // Act - Attempt violation
    try
    {
        await AttemptCrossTenantModification();
    }
    catch (TenantIsolationViolationException)
    {
        // Expected
    }
    
    // Assert
    var logEntries = loggerFactory.GetLogEntries();
    logEntries.Should().Contain(entry => 
        entry.LogLevel == LogLevel.Critical &&
        entry.Message.Contains("TENANT ISOLATION VIOLATION"));
}
```

## 7. Performance Monitoring Examples

### Query Plan Analysis
```csharp
// Example of what gets logged by our performance monitor
/*
[Information] MultiTenant.Enforcer.Performance: Query execution plan analysis
{
  "EntityType": "Task",
  "TenantId": "11111111-1111-1111-1111-111111111111",
  "QueryType": "GetOverdueTasks",
  "IndexesUsed": ["IX_Tasks_TenantId_DueDate", "IX_Tasks_TenantId_Status"],
  "ExecutionTimeMs": 45,
  "RowsScanned": 1250,
  "RowsReturned": 23,
  "TenantFilterApplied": true,
  "Timestamp": "2025-07-13T10:30:00.000Z"
}
*/

[Fact]
public async Task PerformanceMonitor_ShouldLogSlowQueries()
{
    // Arrange
    await GenerateLargeDataset(tenantId: Guid.NewGuid(), 
        projectCount: 5000, tasksPerProject: 200);
    
    var performanceMonitor = new Mock<ITenantPerformanceMonitor>();
    
    // Act
    var tasks = await _taskRepository.GetOverdueTasksAsync();
    
    // Assert
    performanceMonitor.Verify(m => m.LogQueryExecution(
        It.IsAny<string>(), // Query type
        It.IsAny<TimeSpan>(), // Execution time
        It.Is<int>(rows => rows > 0), // Rows returned
        It.IsTrue // Tenant filter applied
    ), Times.Once);
}
```

## 8. Real-World Scenario Testing

### Multi-User Concurrent Access
```csharp
[Fact]
public async Task ConcurrentAccess_DifferentTenants_ShouldNotInterfere()
{
    // Arrange
    var tenant1Tasks = new List<Task<List<ProjectDto>>>();
    var tenant2Tasks = new List<Task<List<ProjectDto>>>();
    
    // Act - Simulate 10 concurrent requests from each tenant
    for (int i = 0; i < 10; i++)
    {
        tenant1Tasks.Add(GetProjectsForTenant("acme"));
        tenant2Tasks.Add(GetProjectsForTenant("techinnovations"));
    }
    
    var allTasks = tenant1Tasks.Cast<Task>().Concat(tenant2Tasks).ToArray();
    await Task.WhenAll(allTasks);
    
    // Assert
    var tenant1Results = await Task.WhenAll(tenant1Tasks);
    var tenant2Results = await Task.WhenAll(tenant2Tasks);
    
    // All results from tenant1 should have same tenant ID
    var tenant1Id = tenant1Results[0][0].TenantId;
    tenant1Results.Should().AllSatisfy(projects =>
        projects.Should().AllSatisfy(p => p.TenantId.Should().Be(tenant1Id)));
    
    // All results from tenant2 should have same tenant ID (different from tenant1)
    var tenant2Id = tenant2Results[0][0].TenantId;
    tenant2Results.Should().AllSatisfy(projects =>
        projects.Should().AllSatisfy(p => p.TenantId.Should().Be(tenant2Id)));
    
    tenant1Id.Should().NotBe(tenant2Id);
}

private async Task<List<ProjectDto>> GetProjectsForTenant(string subdomain)
{
    var client = _factory.CreateClientWithHost($"{subdomain}.localhost");
    return await client.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
}
```

## 9. Visual Studio Experience Examples

### IntelliSense and Error Highlighting
```csharp
public class ExampleController : ControllerBase
{
    private readonly TaskMasterDbContext _context;
    private readonly ITenantRepository<Project> _projectRepo; // ✅ Suggested by IntelliSense

    [HttpGet]
    public async Task<List<Project>> GetProjects()
    {
        // As soon as you type "_context.Projects" you'll see:
        // ❌ Red squiggly underline
        // 💡 Light bulb with "Use ITenantRepository<Project> instead"
        
        return await _context.Projects.ToListAsync();
        //           ^^^^^^^^^^^^^^^^
        //           Error MTI001 highlighted here
    }
}
```

### Code Completion and Suggestions
```csharp
// When you start typing "_projectRepo.", IntelliSense shows:
// ✅ GetByIdAsync(Guid id)
// ✅ GetAllAsync()
// ✅ FindAsync(Expression<Func<Project, bool>> predicate)
// ✅ AddAsync(Project entity)
// ✅ UpdateAsync(Project entity)
// ✅ DeleteAsync(Guid id)

// All methods automatically apply tenant filtering
```

## 10. Common "Gotcha" Scenarios Your Team Will Try

### Attempting to Bypass with Raw SQL
```csharp
[Fact]
public async Task RawSqlQuery_ShouldStillRespectTenantContext()
{
    // Even if someone tries to bypass with raw SQL:
    var projects = await _context.Projects
        .FromSqlRaw("SELECT * FROM Projects WHERE Name LIKE '%test%'")
        .ToListAsync();
    
    // Global query filters STILL apply! They'll only get current tenant's data
    projects.Should().AllSatisfy(p => 
        p.TenantId.Should().Be(_tenantAccessor.Current.TenantId));
}
```

### Trying to Modify TenantId Directly
```csharp
[Fact]
public async Task ModifyTenantId_ShouldThrowException()
{
    // Arrange
    var project = await _projectRepository.GetByIdAsync(existingProjectId);
    
    // Act & Assert
    project.TenantId = Guid.NewGuid(); // Try to change tenant
    
    var exception = await Assert.ThrowsAsync<TenantIsolationViolationException>(
        () => _context.SaveChangesAsync());
        
    exception.Message.Should().Contain("Cross-tenant modification detected");
}
```

This comprehensive test suite demonstrates that your isolation enforcer catches everything your team might try to break, both at compile-time and runtime.
