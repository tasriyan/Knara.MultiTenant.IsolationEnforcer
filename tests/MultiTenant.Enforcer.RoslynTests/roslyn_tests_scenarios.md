### Scenario 1: SAFE DbContext derived from TenantIsolatedDbContext
A db context that is derived from `TenantIsolatedDbContext` is considered SAFE and should compile without issues when used anywhere.
```csharp
public class Company
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
	public void Configure(EntityTypeBuilder<Company> builder)
	{
		builder.ToTable("Companies");
		builder.HasKey(c => c.Id);
		builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
	}
}
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");
		builder.HasKey(p => p.Id);
		builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
	}
}
// THIS SHOULD COMPILE OK BECAUSE SafeDbContext is derived from TenantIsolatedDbContext
public class SafeDbContext(DbContextOptions<SafeDbContext> options,
						ITenantContextAccessor tenantAccessor,
						ILogger<SafeDbContext> logger) : TenantIsolatedDbContext(options, tenantAccessor, logger)
{
	public DbSet<Company> Companies { get; set; }
	public DbSet<Project> Projects { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
	}
}
```

### Scenario 2: SAFE DbContext that does not contains ITenantIsolated implements
A db context that contains only non-tenant-isolated entities (i.e. does not implement `ITenantIsolated`)
is considered SAFE and should compile without issues when used anywhere.
```csharp
public class Company
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
public class AdminAuditLog
{
	public Guid Id { get; set; }
	public string Action { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; }
}
public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
	public void Configure(EntityTypeBuilder<Company> builder)
	{
		builder.ToTable("Companies");
		builder.HasKey(c => c.Id);
		builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
	}
}
public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
	public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
	{
		builder.ToTable("AdminAuditLogs");
		builder.HasKey(a => a.Id);
		builder.Property(a => a.Action).IsRequired().HasMaxLength(500);
		builder.Property(a => a.Timestamp).IsRequired();
	}
}

// THIS SHOULD COMPILE OK BECAUSE SafeDbContext does not contain entities that are tenant-isolated (e.g. ITenantIsolated)
public class SafeDbContext(DbContextOptions<SafeDbContext> options) : DbContext(options)
{
	public DbSet<Company> Companies { get; set; }
	public DbSet<AdminAuditLog> AuditLogs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());
	}
}
```

### Scenario 3: UNSAFE DbContext that contains ITenantIsolated implements and is not derived from TenantIsolatedDbContext
A db context that is not derived from `TenantIsolatedDbContext` is considered UNSAFE and should only be used in scenarios where tenant isolation is not required,
such as operations marked with AllowCrossTenantAccess or when used in a repository that is derived from `TenantIsolatedRepository`.
```csharp
public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");
		builder.HasKey(p => p.Id);
		builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
	}
}

// THIS SHOULD STILL COMPILE OK
public class UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : DbContext(options)
{
	public DbSet<Project> Projects { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
	}
}
```

### Scenario 4: SAFE Repository derived from TenantIsolatedRepository
A repository that is derived from `TenantIsolatedRepository` is considered SAFE even when it uses UNSAFE db context,
and should compile without issues when used anywhere.
```csharp
public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");
		builder.HasKey(p => p.Id);
		builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
	}
}

// THIS SHOULD STILL COMPILE OK
public class UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : DbContext(options)
{
	public DbSet<Project> Projects { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
	}
}
// Uses UNSAFE db context
// HOWEVER: SHOULD COMPILE because it derives from TenantIsolatedRepository THAT ENSURES TENANT ISOLATION
public sealed class SafeRepository(UnsafeDbContext context, 
		ITenantContextAccessor tenantAccessor, 
		ILogger<SafeRepository> logger)
		: TenantIsolatedRepository<Project, UnsafeDbContext>(context, tenantAccessor, logger)
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await GetByIdAsync(id, cancellationToken: default);
	}

	public async Task<List<Project>> GetProjectsByManagerAsync(Guid managerId)
	{
		// Global query filter automatically applies tenant isolation
		return await Query()
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Where(p => p.ProjectManagerId == managerId)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	public async Task<List<Project>> GetProjectsAsync(string filter = "all")
	{
		return filter switch
		{
			"active" => await Query()
									.AsNoTracking()
									.Include(p => p.ProjectManager)
									.Where(p => p.Status == ProjectStatus.Active)
									.OrderBy(p => p.StartDate)
									.ToListAsync(),
			_ => await Query()
										.AsNoTracking()
										.Include(p => p.ProjectManager)
										.OrderBy(p => p.StartDate)
										.ToListAsync(),
		};
	}

	public async Task<Project?> GetProjectWithTasksAsync(Guid projectId)
	{
		return await Query()
			.AsNoTracking()
			.Include(p => p.ProjectManager)
			.Include(p => p.Tasks)
			.ThenInclude(t => t.AssignedTo)
			.FirstOrDefaultAsync(p => p.Id == projectId);
	}

	public async Task AddAsync(Project project)
	{
		await AddAsync(project);
	}
}
```

### # Scenario 5: SAFE Repository that uses TenantIsolatedDbContext implements
A repository that uses a db context derived from `TenantIsolatedDbContext` is considered safe and should compile without issues when used anywhere.
```csharp
public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");
		builder.HasKey(p => p.Id);
		builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
	}
}

public class SafeDbContext(DbContextOptions<SafeDbContext> options,
						ITenantContextAccessor tenantAccessor,
						ILogger<SafeDbContext> logger) : TenantIsolatedDbContext(options, tenantAccessor, logger)
{
	public DbSet<Project> Projects { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
	}
}
// Using dbcontext directly - not using TenantIsolatedRepository
// SHOULD COMPILE because SafeDbContext is derived from TenantIsolatedDbContext THAT ENSURES TENANT ISOLATION
public class SomeSafeService(SafeDbContext context)
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	public async Task<List<Project>> DoSomething(Guid managerId)
	{
		// ... some logic here

		return await context.Projects
			.ToListAsync();
	}
}
```

### Scenario 6: UNSAFE Repository
A repository that is not derived from TenantIsolatedRepository and uses an UNSAFE db context is considered unsafe repository
and should not compile if it contains tenant-isolated entities.
```csharp
public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");
		builder.HasKey(p => p.Id);
		builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
	}
}

// THIS SHOULD STILL COMPILE OK
public class UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : DbContext(options)
{
	public DbSet<Project> Projects { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new ProjectConfiguration());
	}
}

// Uses UNSAFE db context
// SHOULD NOT COMPILE because it does not derive from TenantIsolatedRepository or uses a dbcontext derived from TenantIsolatedDbContext
// ERRORS EXPECTED: Either MTI001 or MTI004 or MTI006
public class UnsafeRepository(UnsafeDbContext context)
{
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	public async Task AddAsync(Project project)
	{
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}
}
```

### Scenario 7: Using CrossTenantOperationManager with SAFE DbContext
```csharp

[AllowCrossTenantAccess("System admin needs to view all companies", "SystemAdmin")]
public sealed class GetAllCompaniesUnsafe : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/companies",
			async (ICrossTenantOperationManager crossTenantManager,
					SafeDbContext context) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					// In system context, we can access the non-tenant-isolated Companies table
					var companies = await context.Companies
													.AsNoTracking()
													.OrderBy(c => c.Name)
													.ToListAsync();

					return Results.Ok(companies).ToList());
				}, "Admin viewing all companies");
			});
	}
}
```

### Scenario 8: Using minimal endpoints with SAFE DbContext
```csharp
public sealed class GetProjects : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects",
			async (string? filter,
					SafeDbContext dbContext) =>
			{
				var projects = await dbContext.Projects.ToListAsync();
				return Results.Ok(projects.ToList());
			});
	}
}
```

### Scenario 9: Using minimal endpoints with UNSAFE DbContext
```csharp
// Using UNSAFE DbContext directly in minimal endpoint - SHOULD NOT COMPILE
// ERRORS EXPECTED: Either MTI001 or MTI004 or MTI006
public sealed class GetUnsafeProjects : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/unsafe/projects",
			async (UnsafeDbContext dbContext) =>
			{
				var projects = await dbContext.Projects.ToListAsync();
				return Results.Ok(projects);
			});
	}
}
```

### Scenario 10: Using regular classes with SAFE repository
```csharp
public class ProjectService
{
	private readonly SafeRepository _repository;
	public ProjectService(SafeRepository repository)
	{
		_repository = repository;
	}
	public async Task<Project?> GetProjectByIdAsync(Guid id)
	{
		return await _repository.GetByIdAsync(id);
	}
	public async Task<List<Project>> GetAllProjectsAsync()
	{
		return await _repository.GetProjectsAsync();
	}
}
```


