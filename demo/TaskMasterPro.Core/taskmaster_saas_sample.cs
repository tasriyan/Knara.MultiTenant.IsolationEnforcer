# TaskMaster Pro - Multi-Tenant SaaS Sample Application

## Project Structure
```
TaskMasterPro.SaaS/
├── src/
│   ├── TaskMasterPro.Core/              # Domain models and interfaces
│   ├── TaskMasterPro.Infrastructure/    # Data access with Postgres
│   ├── TaskMasterPro.Api/              # Web API controllers
│   ├── TaskMasterPro.Tests/            # Unit and integration tests
│   └── MultiTenant.Enforcer/           # Our isolation enforcer library
└── docker-compose.yml                  # Postgres setup
```

## 1. Domain Models (TaskMasterPro.Core)

### Core Entities
```csharp
// TaskMasterPro.Core/Entities/Company.cs
public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty; // For subdomain-based tenant resolution
    public DateTime CreatedAt { get; set; }
    public CompanyTier Tier { get; set; } = CompanyTier.Starter;
    public bool IsActive { get; set; } = true;
}

public enum CompanyTier
{
    Starter = 1,
    Professional = 2,
    Enterprise = 3
}

// TaskMasterPro.Core/Entities/User.cs
public class User : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; } // Company ID
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public string FullName => $"{FirstName} {LastName}";
}

public enum UserRole
{
    Member = 1,
    ProjectManager = 2,
    Admin = 3
}

// TaskMasterPro.Core/Entities/Project.cs
public class Project : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectManagerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public User ProjectManager { get; set; } = null!;
    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}

public enum ProjectStatus
{
    Planning = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}

// TaskMasterPro.Core/Entities/Task.cs
public class Task : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedToId { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public User? AssignedTo { get; set; }
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TaskStatus
{
    ToDo = 1,
    InProgress = 2,
    InReview = 3,
    Done = 4
}

// TaskMasterPro.Core/Entities/TimeEntry.cs
public class TimeEntry : ITenantIsolated
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Task Task { get; set; } = null!;
    public User User { get; set; } = null!;
}

// TaskMasterPro.Core/Entities/AdminAuditLog.cs
// This entity needs cross-tenant access for system administrators
public class AdminAuditLog : ICrossTenantAccessible
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
}
```

## 2. Infrastructure Layer with Postgres (TaskMasterPro.Infrastructure)

### DbContext Implementation
```csharp
// TaskMasterPro.Infrastructure/Data/TaskMasterDbContext.cs
public class TaskMasterDbContext : TenantDbContext
{
    public DbSet<Company> Companies { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Task> Tasks { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    public TaskMasterDbContext(DbContextOptions<TaskMasterDbContext> options, 
        ITenantContextAccessor tenantAccessor, 
        ILogger<TaskMasterDbContext> logger) 
        : base(options, tenantAccessor, logger)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Apply tenant isolation

        ConfigureEntities(modelBuilder);
        SeedData(modelBuilder);
    }

    private void ConfigureEntities(ModelBuilder modelBuilder)
    {
        // Company configuration
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Domain).IsUnique();
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            
            // Composite index for performance
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Role });
        });

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            
            entity.HasOne(e => e.ProjectManager)
                  .WithMany()
                  .HasForeignKey(e => e.ProjectManagerId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.ProjectManagerId });
        });

        // Task configuration
        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.AssignedTo)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedToId)
                  .OnDelete(DeleteBehavior.SetNull);
                  
            entity.HasIndex(e => new { e.TenantId, e.ProjectId });
            entity.HasIndex(e => new { e.TenantId, e.AssignedToId });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.DueDate });
        });

        // TimeEntry configuration
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            
            entity.HasOne(e => e.Task)
                  .WithMany(t => t.TimeEntries)
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasIndex(e => new { e.TenantId, e.UserId });
            entity.HasIndex(e => new { e.TenantId, e.TaskId });
            entity.HasIndex(e => new { e.TenantId, e.StartTime });
        });

        // AdminAuditLog configuration
        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            
            entity.HasIndex(e => new { e.TenantId, e.Timestamp });
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.UserId);
        });
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed test companies
        var company1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var company2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<Company>().HasData(
            new Company
            {
                Id = company1Id,
                Name = "Acme Corporation",
                Domain = "acme",
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                Tier = CompanyTier.Professional,
                IsActive = true
            },
            new Company
            {
                Id = company2Id,
                Name = "Tech Innovations Inc",
                Domain = "techinnovations",
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                Tier = CompanyTier.Enterprise,
                IsActive = true
            }
        );

        // Seed test users
        modelBuilder.Entity<User>().HasData(
            // Acme users
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
                TenantId = company1Id,
                Email = "john.doe@acme.com",
                FirstName = "John",
                LastName = "Doe",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow.AddDays(-85),
                IsActive = true
            },
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
                TenantId = company1Id,
                Email = "jane.smith@acme.com",
                FirstName = "Jane",
                LastName = "Smith",
                Role = UserRole.ProjectManager,
                CreatedAt = DateTime.UtcNow.AddDays(-80),
                IsActive = true
            },
            // Tech Innovations users
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId = company2Id,
                Email = "bob.wilson@techinnovations.com",
                FirstName = "Bob",
                LastName = "Wilson",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow.AddDays(-55),
                IsActive = true
            }
        );
    }
}
```

### Repository Implementations
```csharp
// TaskMasterPro.Infrastructure/Repositories/ProjectRepository.cs
public interface IProjectRepository : ITenantRepository<Project>
{
    Task<List<Project>> GetProjectsByManagerAsync(Guid managerId);
    Task<List<Project>> GetActiveProjectsAsync();
    Task<Project?> GetProjectWithTasksAsync(Guid projectId);
}

public class ProjectRepository : TenantRepository<Project>, IProjectRepository
{
    public ProjectRepository(TaskMasterDbContext context, ITenantContextAccessor tenantAccessor) 
        : base(context, tenantAccessor)
    {
    }

    public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
    {
        // Global query filter automatically applies tenant isolation
        return await Context.Projects
            .Include(p => p.ProjectManager)
            .Where(p => p.ProjectManagerId == managerId)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Project>> GetActiveProjectsAsync()
    {
        return await Context.Projects
            .Include(p => p.ProjectManager)
            .Where(p => p.Status == ProjectStatus.Active)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
    {
        return await Context.Projects
            .Include(p => p.ProjectManager)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.AssignedTo)
            .FirstOrDefaultAsync(p => p.Id == projectId);
    }
}

// TaskMasterPro.Infrastructure/Repositories/TaskRepository.cs
public interface ITaskRepository : ITenantRepository<Task>
{
    Task<List<Task>> GetTasksByProjectAsync(Guid projectId);
    Task<List<Task>> GetTasksByUserAsync(Guid userId);
    Task<List<Task>> GetOverdueTasksAsync();
    Task<Dictionary<TaskStatus, int>> GetTaskCountsByStatusAsync();
}

public class TaskRepository : TenantRepository<Task>, ITaskRepository
{
    public TaskRepository(TaskMasterDbContext context, ITenantContextAccessor tenantAccessor) 
        : base(context, tenantAccessor)
    {
    }

    public async Task<List<Task>> GetTasksByProjectAsync(Guid projectId)
    {
        return await Context.Tasks
            .Include(t => t.AssignedTo)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<List<Task>> GetTasksByUserAsync(Guid userId)
    {
        return await Context.Tasks
            .Include(t => t.Project)
            .Where(t => t.AssignedToId == userId)
            .Where(t => t.Status != TaskStatus.Done)
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<List<Task>> GetOverdueTasksAsync()
    {
        var today = DateTime.UtcNow.Date;
        
        return await Context.Tasks
            .Include(t => t.AssignedTo)
            .Include(t => t.Project)
            .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date < today)
            .Where(t => t.Status != TaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<Dictionary<TaskStatus, int>> GetTaskCountsByStatusAsync()
    {
        return await Context.Tasks
            .GroupBy(t => t.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }
}
```

## 3. API Controllers (TaskMasterPro.Api)

### Controllers with Proper Tenant Isolation
```csharp
// TaskMasterPro.Api/Controllers/ProjectsController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectRepository projectRepository, 
        ITenantContextAccessor tenantAccessor,
        ILogger<ProjectsController> logger)
    {
        _projectRepository = projectRepository;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetProjects()
    {
        var projects = await _projectRepository.GetAllAsync();
        
        _logger.LogInformation("Retrieved {Count} projects for tenant {TenantId}", 
            projects.Count, _tenantAccessor.Current.TenantId);
            
        return Ok(projects.Select(ProjectDto.FromEntity).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid id)
    {
        var project = await _projectRepository.GetProjectWithTasksAsync(id);
        
        if (project == null)
        {
            return NotFound();
        }
        
        return Ok(ProjectDto.FromEntity(project));
    }

    [HttpPost]
    [Authorize(Policy = "ProjectManager")]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectDto dto)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            ProjectManagerId = dto.ProjectManagerId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedAt = DateTime.UtcNow
            // TenantId is automatically set by the TenantDbContext
        };

        await _projectRepository.AddAsync(project);
        
        _logger.LogInformation("Created project {ProjectId} for tenant {TenantId}", 
            project.Id, _tenantAccessor.Current.TenantId);
            
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, 
            ProjectDto.FromEntity(project));
    }

    // This method demonstrates what would be caught by our analyzer
    /* ANALYZER VIOLATION EXAMPLE:
    [HttpGet("bad-example")]
    public async Task<ActionResult> BadExample()
    {
        // This would trigger MTI001 error: Direct DbSet access on tenant-isolated entity
        var projects = await _context.Set<Project>().ToListAsync();
        return Ok(projects);
    }
    */
}

// TaskMasterPro.Api/Controllers/TasksController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITenantContextAccessor _tenantAccessor;

    public TasksController(ITaskRepository taskRepository, ITenantContextAccessor tenantAccessor)
    {
        _taskRepository = taskRepository;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("my-tasks")]
    public async Task<ActionResult<List<TaskDto>>> GetMyTasks()
    {
        var userId = GetCurrentUserId();
        var tasks = await _taskRepository.GetTasksByUserAsync(userId);
        
        return Ok(tasks.Select(TaskDto.FromEntity).ToList());
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<List<TaskDto>>> GetOverdueTasks()
    {
        var tasks = await _taskRepository.GetOverdueTasksAsync();
        return Ok(tasks.Select(TaskDto.FromEntity).ToList());
    }

    [HttpGet("project/{projectId}")]
    public async Task<ActionResult<List<TaskDto>>> GetTasksByProject(Guid projectId)
    {
        var tasks = await _taskRepository.GetTasksByProjectAsync(projectId);
        return Ok(tasks.Select(TaskDto.FromEntity).ToList());
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> UpdateTaskStatus(Guid id, UpdateTaskStatusDto dto)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        
        if (task == null)
        {
            return NotFound();
        }

        task.Status = dto.Status;
        if (dto.Status == TaskStatus.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
        }

        await _taskRepository.UpdateAsync(task);
        
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException("User ID not found in token"));
    }
}
```

### Cross-Tenant Admin Operations
```csharp
// TaskMasterPro.Api/Controllers/AdminController.cs
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "SystemAdmin")]
public class AdminController : ControllerBase
{
    private readonly ICrossTenantOperationManager _crossTenantManager;
    private readonly IAdminAuditLogRepository _auditLogRepository;
    private readonly TaskMasterDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ICrossTenantOperationManager crossTenantManager,
        IAdminAuditLogRepository auditLogRepository,
        TaskMasterDbContext context,
        ILogger<AdminController> logger)
    {
        _crossTenantManager = crossTenantManager;
        _auditLogRepository = auditLogRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet("companies")]
    [AllowCrossTenantAccess("System admin needs to view all companies", "SystemAdmin")]
    public async Task<ActionResult<List<CompanyDto>>> GetAllCompanies()
    {
        return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
        {
            // In system context, we can access the non-tenant-isolated Companies table
            var companies = await _context.Companies
                .OrderBy(c => c.Name)
                .ToListAsync();
                
            return Ok(companies.Select(CompanyDto.FromEntity).ToList());
        }, "Admin viewing all companies");
    }

    [HttpGet("audit-logs")]
    [AllowCrossTenantAccess("System admin needs cross-tenant audit access", "SystemAdmin")]
    public async Task<ActionResult<List<AdminAuditLogDto>>> GetAuditLogs(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] int take = 100)
    {
        return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
        {
            var query = _context.AdminAuditLogs.AsQueryable();
            
            if (tenantId.HasValue)
            {
                query = query.Where(log => log.TenantId == tenantId.Value);
            }
            
            if (fromDate.HasValue)
            {
                query = query.Where(log => log.Timestamp >= fromDate.Value);
            }
            
            var logs = await query
                .OrderByDescending(log => log.Timestamp)
                .Take(take)
                .ToListAsync();
                
            return Ok(logs.Select(AdminAuditLogDto.FromEntity).ToList());
        }, $"Admin viewing audit logs for tenant {tenantId}");
    }

    [HttpPost("migrate-user")]
    [AllowCrossTenantAccess("System admin can migrate users between tenants", "SystemAdmin")]
    public async Task<ActionResult> MigrateUser(MigrateUserDto dto)
    {
        return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Find user in source tenant
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == dto.UserId && u.TenantId == dto.FromTenantId);
                    
                if (user == null)
                {
                    return NotFound("User not found in source tenant");
                }
                
                // Update user's tenant
                user.TenantId = dto.ToTenantId;
                
                // Log the migration
                var auditLog = new AdminAuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = dto.FromTenantId,
                    Action = "USER_MIGRATION",
                    EntityType = nameof(User),
                    EntityId = user.Id,
                    UserEmail = GetCurrentUserEmail(),
                    Details = $"Migrated user {user.Email} from {dto.FromTenantId} to {dto.ToTenantId}",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = GetClientIpAddress()
                };
                
                _context.AdminAuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                _logger.LogWarning("User {UserId} migrated from tenant {FromTenant} to {ToTenant} by {AdminEmail}",
                    dto.UserId, dto.FromTenantId, dto.ToTenantId, GetCurrentUserEmail());
                
                return Ok(new { Message = "User migrated successfully" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to migrate user {UserId}", dto.UserId);
                throw;
            }
        }, $"User migration from {dto.FromTenantId} to {dto.ToTenantId}");
    }

    [HttpGet("tenant-statistics")]
    [AllowCrossTenantAccess("System admin needs tenant usage statistics", "SystemAdmin")]
    public async Task<ActionResult<List<TenantStatisticsDto>>> GetTenantStatistics()
    {
        return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
        {
            var statistics = await _context.Companies
                .Select(company => new TenantStatisticsDto
                {
                    TenantId = company.Id,
                    CompanyName = company.Name,
                    Tier = company.Tier,
                    UserCount = _context.Users.Count(u => u.TenantId == company.Id),
                    ProjectCount = _context.Projects.Count(p => p.TenantId == company.Id),
                    TaskCount = _context.Tasks.Count(t => t.TenantId == company.Id),
                    CreatedAt = company.CreatedAt,
                    IsActive = company.IsActive
                })
                .ToListAsync();
                
            return Ok(statistics);
        }, "Admin retrieving tenant statistics");
    }

    private string GetCurrentUserEmail()
    {
        return User.FindFirst("email")?.Value ?? "system";
    }

    private string GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

## 4. Startup Configuration and Middleware

### Program.cs
```csharp
// TaskMasterPro.Api/Program.cs
using TaskMasterPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MultiTenant.Enforcer.AspNetCore;
using MultiTenant.Enforcer.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
builder.Services.AddDbContext<TaskMasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            npgsqlOptions.CommandTimeout(30);
        }));

// Multi-tenant isolation enforcer
builder.Services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
{
    options.DefaultTenantResolver = typeof(SubdomainTenantResolver);
    options.EnablePerformanceMonitoring = true;
    options.LogViolations = true;
});

// Authentication & Authorization
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"];
        options.ApiName = "taskmaster_api";
        options.RequireHttpsMetadata = false; // Dev only
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProjectManager", policy =>
        policy.RequireClaim("role", "ProjectManager", "Admin"));
        
    options.AddPolicy("SystemAdmin", policy =>
        policy.RequireClaim("role", "SystemAdmin"));
});

// Register repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAdminAuditLogRepository, AdminAuditLogRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CRITICAL: Multi-tenant middleware must come before authentication
app.UseMultiTenantIsolation();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
```

### Custom Tenant Resolver
```csharp
// TaskMasterPro.Infrastructure/TenantResolution/SubdomainTenantResolver.cs
public class SubdomainTenantResolver : ITenantResolver
{
    private readonly TaskMasterDbContext _context;
    private readonly ILogger<SubdomainTenantResolver> _logger;
    private readonly IMemoryCache _cache;

    public SubdomainTenantResolver(TaskMasterDbContext context, 
        ILogger<SubdomainTenantResolver> logger,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<TenantContext> ResolveTenantAsync(HttpContext httpContext)
    {
        // Check for system admin token first
        if (httpContext.User.HasClaim("role", "SystemAdmin"))
        {
            return TenantContext.SystemContext("SystemAdmin");
        }

        // Extract subdomain from host
        var host = httpContext.Request.Host.Host;
        var subdomain = ExtractSubdomain(host);
        
        if (string.IsNullOrEmpty(subdomain))
        {
            throw new TenantResolutionException("No subdomain found in request");
        }

        // Check cache first
        var cacheKey = $"tenant_domain_{subdomain}";
        if (_cache.TryGetValue(cacheKey, out Guid cachedTenantId))
        {
            return TenantContext.ForTenant(cachedTenantId, $"Subdomain:{subdomain}");
        }

        // Look up company by domain
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Domain == subdomain && c.IsActive);
            
        if (company == null)
        {
            throw new TenantResolutionException($"No active company found for domain: {subdomain}");
        }

        // Cache for 5 minutes
        _cache.Set(cacheKey, company.Id, TimeSpan.FromMinutes(5));
        
        _logger.LogDebug("Resolved tenant {TenantId} for subdomain {Subdomain}", 
            company.Id, subdomain);
            
        return TenantContext.ForTenant(company.Id, $"Subdomain:{subdomain}");
    }

    private string ExtractSubdomain(string host)
    {
        // For local development: acme.localhost:5000 -> acme
        // For production: acme.taskmaster.com -> acme
        
        var parts = host.Split('.');
        
        if (parts.Length >= 2 && parts[0] != "www")
        {
            return parts[0];
        }
        
        return string.Empty;
    }
}
```

## 5. Docker Compose for Development

### docker-compose.yml
```yaml
# docker-compose.yml
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: taskmaster_dev
      POSTGRES_USER: taskmaster
      POSTGRES_PASSWORD: dev_password_123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U taskmaster"]
      interval: 30s
      timeout: 10s
      retries: 3

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  postgres_data:
```

### scripts/init-db.sql
```sql
-- scripts/init-db.sql
-- Create extensions for performance
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Create additional indexes for performance testing
-- These will be created by EF migrations, but good to have for manual testing
```

## 6. Integration Tests

### Test Examples
```csharp
// TaskMasterPro.Tests/Integration/TenantIsolationTests.cs
public class TenantIsolationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TaskMasterDbContext _context;

    public TenantIsolationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _context = CreateDbContext();
    }

    [Fact]
    public async Task GetProjects_ShouldOnlyReturnCurrentTenantProjects()
    {
        // Arrange
        var tenant1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenant2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        
        await SeedTestData(tenant1Id, tenant2Id);
        
        var client = _factory.WithTenantContext(tenant1Id).CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/projects");
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        projects.Should().NotBeNull();
        projects.Should().HaveCount(2); // Only tenant1 projects
        projects.Should().AllSatisfy(p => p.TenantId.Should().Be(tenant1Id));
    }

    [Fact]
    public async Task CreateProject_ShouldAutomaticallySetTenantId()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var client = _factory.WithTenantContext(tenantId).CreateClient();
        
        var createDto = new CreateProjectDto
        {
            Name = "Test Project",
            Description = "Test Description",
            ProjectManagerId = Guid.Parse("11111111-1111-1111-1111-111111111112"),
            StartDate = DateTime.UtcNow
        };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/projects", createDto);
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        project.Should().NotBeNull();
        project.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CrossTenantOperation_WithoutSystemContext_ShouldFail()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var client = _factory.WithTenantContext(tenantId).CreateClient();
        
        // Act & Assert
        var response = await client.GetAsync("/api/admin/companies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SystemAdmin_ShouldAccessCrossTenantData()
    {
        // Arrange
        var client = _factory.WithSystemAdminContext().CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/admin/companies");
        var companies = await response.Content.ReadFromJsonAsync<List<CompanyDto>>();
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        companies.Should().NotBeNull();
        companies.Should().HaveCountGreaterThan(0);
    }

    private async Task SeedTestData(Guid tenant1Id, Guid tenant2Id)
    {
        // Create test projects for both tenants
        var projects = new[]
        {
            new Project { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Project 1", CreatedAt = DateTime.UtcNow },
            new Project { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Project 2", CreatedAt = DateTime.UtcNow },
            new Project { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Project 1", CreatedAt = DateTime.UtcNow },
        };
        
        _context.Projects.AddRange(projects);
        await _context.SaveChangesAsync();
    }
}
```

## 7. Performance Test Data Generator

### Load Testing Script
```csharp
// TaskMasterPro.Tests/Performance/DataGenerator.cs
public class PerformanceDataGenerator
{
    private readonly TaskMasterDbContext _context;
    
    public async Task GenerateLargeDatasetAsync(int companiesCount = 100, int usersPerCompany = 50, 
        int projectsPerCompany = 20, int tasksPerProject = 100)
    {
        var companies = GenerateCompanies(companiesCount);
        _context.Companies.AddRange(companies);
        await _context.SaveChangesAsync();
        
        foreach (var company in companies)
        {
            var users = GenerateUsers(company.Id, usersPerCompany);
            _context.Users.AddRange(users);
            
            var projects = GenerateProjects(company.Id, users, projectsPerCompany);
            _context.Projects.AddRange(projects);
            
            foreach (var project in projects)
            {
                var tasks = GenerateTasks(company.Id, project.Id, users, tasksPerProject);
                _context.Tasks.AddRange(tasks);
            }
            
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"Generated data for company {company.Name}");
        }
    }
    
    private List<Company> GenerateCompanies(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company {i}",
                Domain = $"company{i}",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 365)),
                Tier = (CompanyTier)Random.Shared.Next(1, 4),
                IsActive = true
            })
            .ToList();
    }
    
    // Additional generation methods...
}
```

## Configuration Files

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=taskmaster_dev;Username=taskmaster;Password=dev_password_123"
  },
  "IdentityServer": {
    "Authority": "https://localhost:5001"
  },
  "MultiTenant": {
    "EnableViolationLogging": true,
    "CacheTenantResolution": true,
    "CacheExpirationMinutes": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "TaskMasterPro": "Debug",
      "MultiTenant.Enforcer": "Information"
    }
  }
}
```

## Testing Scenarios

This sample application tests:

1. **Basic Tenant Isolation**: Projects, tasks, users are automatically filtered by tenant
2. **Performance with Indexes**: Large datasets with proper tenant-based indexing
3. **Cross-Tenant Operations**: Admin functions that legitimately need cross-tenant access
4. **Analyzer Violations**: Examples of code that would trigger compile-time errors
5. **Runtime Protection**: SaveChanges validation and automatic tenant ID assignment
6. **Multi-Tier Architecture**: Different feature sets based on company tier
7. **Audit Logging**: Cross-tenant audit logs for compliance

## How to Run and Test

1. **Start Infrastructure**: `docker-compose up -d`
2. **Run Migrations**: `dotnet ef database update`
3. **Generate Test Data**: Run the performance data generator
4. **Test Scenarios**: 
   - Access `acme.localhost:5000/api/projects` (tenant 1)
   - Access `techinnovations.localhost:5000/api/projects` (tenant 2)
   - Try admin endpoints without proper authorization
   - Monitor logs for tenant isolation events

This gives you a complete, realistic SaaS application that thoroughly tests every aspect of the multi-tenant isolation enforcer.
