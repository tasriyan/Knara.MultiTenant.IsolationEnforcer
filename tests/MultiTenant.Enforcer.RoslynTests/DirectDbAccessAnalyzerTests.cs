using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

/// <summary>
/// Tests for DirectDbAccessAnalyzer (MTI001) covering scenarios from roslyn_tests_scenarios.md
/// </summary>
public class DirectDbAccessAnalyzerTests
{
	private const string BaseTestCode = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Multitenant.Enforcer.Core
{
    public interface ITenantIsolated 
    {
        Guid TenantId { get; set; }
    }
    
    public interface ITenantContextAccessor
    {
        // Placeholder interface
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AllowCrossTenantAccessAttribute : Attribute
    {
        public AllowCrossTenantAccessAttribute(string justification, string role = null) { }
    }
}

namespace Multitenant.Enforcer.EntityFramework
{
    public abstract class TenantDbContext : DbContext
    {
        protected TenantDbContext(DbContextOptions options, Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor, ILogger logger) : base(options) { }
    }
    
    public abstract class TenantRepository<TEntity, TContext> where TEntity : class where TContext : DbContext
    {
        protected TContext Context { get; }
        protected TenantRepository(TContext context, Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor, ILogger logger) 
        {
            Context = context;
        }
        
        protected virtual IQueryable<TEntity> Query() => Context.Set<TEntity>();
        protected virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await Query().FirstOrDefaultAsync(cancellationToken);
        protected virtual async Task AddAsync(TEntity entity) => await Context.Set<TEntity>().AddAsync(entity);
    }
}

namespace Multitenant.Enforcer.AspnetCore
{
    public interface ICrossTenantOperationManager
    {
        Task<T> ExecuteCrossTenantOperationAsync<T>(Func<Task<T>> operation, string operationDescription);
        Task ExecuteCrossTenantOperationAsync(Func<Task> operation, string operationDescription);
    }
}

namespace TestApp.Entities
{
    // Non-tenant isolated entity
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
    
    // Tenant-isolated entity
    public class Project : Multitenant.Enforcer.Core.ITenantIsolated
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
    
    // Non-tenant isolated entity
    public class AdminAuditLog
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}

namespace TestApp.Configurations
{
    public class CompanyConfiguration : IEntityTypeConfiguration<TestApp.Entities.Company>
    {
        public void Configure(EntityTypeBuilder<TestApp.Entities.Company> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        }
    }
    
    public class ProjectConfiguration : IEntityTypeConfiguration<TestApp.Entities.Project>
    {
        public void Configure(EntityTypeBuilder<TestApp.Entities.Project> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        }
    }
    
    public class AdminAuditLogConfiguration : IEntityTypeConfiguration<TestApp.Entities.AdminAuditLog>
    {
        public void Configure(EntityTypeBuilder<TestApp.Entities.AdminAuditLog> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(500);
            builder.Property(a => a.Timestamp).IsRequired();
        }
    }
}
";

	#region Scenario 1: SAFE DbContext derived from TenantDbContext

	[Fact]
	public async Task Scenario1_SafeDbContext_DerivedFromTenantDbContext_ShouldNotReportDiagnostic()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // SAFE: Derived from TenantDbContext
    public class SafeDbContext : Multitenant.Enforcer.EntityFramework.TenantDbContext
    {
        public SafeDbContext(DbContextOptions<SafeDbContext> options,
                            Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                            ILogger<SafeDbContext> logger) : base(options, tenantAccessor, logger) { }
        
        public DbSet<TestApp.Entities.Company> Companies { get; set; }
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.CompanyConfiguration());
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Services
{
    public class SafeService
    {
        public async Task<TestApp.Entities.Project?> GetProjectAsync(TestApp.Data.SafeDbContext context, Guid id)
        {
            // This should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
            return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
        }
        
        public async Task<List<TestApp.Entities.Project>> GetAllProjectsAsync(TestApp.Data.SafeDbContext context)
        {
            // This should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
            return await context.Projects.ToListAsync();
        }
        
        public async Task<TestApp.Entities.Project?> GetProjectUsingSetAsync(TestApp.Data.SafeDbContext context, Guid id)
        {
            // This should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
            return await context.Set<TestApp.Entities.Project>().FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 2: SAFE DbContext that does not contain ITenantIsolated entities

	[Fact]
	public async Task Scenario2_SafeDbContext_NoTenantIsolatedEntities_ShouldNotReportDiagnostic()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // SAFE: No tenant-isolated entities
    public class SafeDbContext : DbContext
    {
        public SafeDbContext(DbContextOptions<SafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Company> Companies { get; set; }
        public DbSet<TestApp.Entities.AdminAuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.AdminAuditLogConfiguration());
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.CompanyConfiguration());
        }
    }
}

namespace TestApp.Services
{
    public class SafeService
    {
        public async Task<TestApp.Entities.Company?> GetCompanyAsync(TestApp.Data.SafeDbContext context, Guid id)
        {
            // This should NOT trigger MTI001 because Company is not tenant-isolated
            return await context.Companies.FirstOrDefaultAsync(c => c.Id == id);
        }
        
        public async Task<List<TestApp.Entities.AdminAuditLog>> GetAuditLogsAsync(TestApp.Data.SafeDbContext context)
        {
            // This should NOT trigger MTI001 because AdminAuditLog is not tenant-isolated
            return await context.AuditLogs.ToListAsync();
        }
        
        public async Task<TestApp.Entities.Company?> GetCompanyUsingSetAsync(TestApp.Data.SafeDbContext context, Guid id)
        {
            // This should NOT trigger MTI001 because Company is not tenant-isolated
            return await context.Set<TestApp.Entities.Company>().FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 3: UNSAFE DbContext with ITenantIsolated entities not derived from TenantDbContext

	[Fact]
	public async Task Scenario3_UnsafeDbContext_WithTenantIsolatedEntities_ShouldCompileButClassShouldBeDetectedByOtherAnalyzer()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // UNSAFE: Contains tenant-isolated entities but not derived from TenantDbContext
    // Note: This should be caught by TenantDbContextAnalyzer (MTI006), not DirectDbAccessAnalyzer
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}";

		// The DirectDbAccessAnalyzer should NOT report any diagnostics for the class declaration itself
		// That's the job of TenantDbContextAnalyzer (MTI006)
		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 4: SAFE Repository derived from TenantRepository

	[Fact]
	public async Task Scenario4_SafeRepository_DerivedFromTenantRepository_ShouldNotReportDiagnostic()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Repositories
{
    // SAFE: Derived from TenantRepository
    public sealed class SafeRepository : Multitenant.Enforcer.EntityFramework.TenantRepository<TestApp.Entities.Project, TestApp.Data.UnsafeDbContext>
    {
        public SafeRepository(TestApp.Data.UnsafeDbContext context,
                             Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                             ILogger<SafeRepository> logger)
            : base(context, tenantAccessor, logger) { }

        public async Task<TestApp.Entities.Project?> GetByIdAsync(Guid id)
        {
            return await GetByIdAsync(id, cancellationToken: default);
        }

        public async Task<List<TestApp.Entities.Project>> GetProjectsAsync()
        {
            // Should NOT trigger MTI001 because we're in a TenantRepository
            return await Query().AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        }

        public async Task AddAsync(TestApp.Entities.Project project)
        {
            await AddAsync(project);
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 5: SAFE Repository using TenantDbContext

	[Fact]
	public async Task Scenario5_SafeService_UsingTenantDbContext_ShouldNotReportDiagnostic()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class SafeDbContext : Multitenant.Enforcer.EntityFramework.TenantDbContext
    {
        public SafeDbContext(DbContextOptions<SafeDbContext> options,
                            Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                            ILogger<SafeDbContext> logger) : base(options, tenantAccessor, logger) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Services
{
    // SAFE: Using TenantDbContext directly
    public class SomeSafeService
    {
        private readonly TestApp.Data.SafeDbContext _context;
        
        public SomeSafeService(TestApp.Data.SafeDbContext context)
        {
            _context = context;
        }

        public async Task<TestApp.Entities.Project?> GetByIdAsync(Guid id)
        {
            // Should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
            return await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<TestApp.Entities.Project>> DoSomething(Guid managerId)
        {
            // Should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
            return await _context.Projects.ToListAsync();
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 6: UNSAFE Repository - Should Report MTI001

	[Fact]
	public async Task Scenario6_UnsafeRepository_DirectDbAccess_ShouldReportMTI001()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Repositories
{
    // UNSAFE: Not derived from TenantRepository and using unsafe DbContext
    public class UnsafeRepository
    {
        private readonly TestApp.Data.UnsafeDbContext _context;
        
        public UnsafeRepository(TestApp.Data.UnsafeDbContext context)
        {
            _context = context;
        }

        public async Task<TestApp.Entities.Project?> GetByIdAsync(Guid id)
        {
            // Should trigger MTI001 - direct access to tenant-isolated entity through unsafe DbContext
            return await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddAsync(TestApp.Entities.Project project)
        {
            // Should trigger MTI001 - direct access to tenant-isolated entity through unsafe DbContext
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
        }
        
        public async Task<TestApp.Entities.Project?> GetUsingSetAsync(Guid id)
        {
            // Should trigger MTI001 - direct Set<T>() access to tenant-isolated entity through unsafe DbContext
            return await _context.Set<TestApp.Entities.Project>().FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}";

		var expected = new[]
		{
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(150, 26, 150, 43)
				.WithArguments("Project"),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(156, 13, 156, 30)
				.WithArguments("Project"),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(163, 26, 163, 64)
				.WithArguments("Project")
		};

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region Scenario 7: Using CrossTenantOperationManager with SAFE DbContext

	[Fact]
	public async Task Scenario7_CrossTenantOperationManager_WithSafeDbContext_ShouldNotReportMTI001()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class SafeDbContext : Multitenant.Enforcer.EntityFramework.TenantDbContext
    {
        public SafeDbContext(DbContextOptions<SafeDbContext> options,
                            Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                            ILogger<SafeDbContext> logger) : base(options, tenantAccessor, logger) { }
        
        public DbSet<TestApp.Entities.Company> Companies { get; set; }
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.CompanyConfiguration());
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Endpoints
{
    [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""System admin needs to view all companies"", ""SystemAdmin"")]
    public sealed class GetAllCompaniesUnsafe
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/admin/companies"",
                async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager,
                       TestApp.Data.SafeDbContext context) =>
                {
                    return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                    {
                        // Should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
                        var companies = await context.Companies
                                                .AsNoTracking()
                                                .OrderBy(c => c.Name)
                                                .ToListAsync();

                        return Results.Ok(companies);
                    }, ""Admin viewing all companies"");
                });
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 8: Using minimal endpoints with SAFE DbContext

	[Fact]
	public async Task Scenario8_MinimalEndpoints_WithSafeDbContext_ShouldNotReportMTI001()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class SafeDbContext : Multitenant.Enforcer.EntityFramework.TenantDbContext
    {
        public SafeDbContext(DbContextOptions<SafeDbContext> options,
                            Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                            ILogger<SafeDbContext> logger) : base(options, tenantAccessor, logger) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Endpoints
{
    public sealed class GetProjects
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/projects"",
                async (string? filter, TestApp.Data.SafeDbContext dbContext) =>
                {
                    // Should NOT trigger MTI001 because SafeDbContext derives from TenantDbContext
                    var projects = await dbContext.Projects.ToListAsync();
                    return Results.Ok(projects);
                });
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 9: Using minimal endpoints with UNSAFE DbContext

	[Fact]
	public async Task Scenario9_MinimalEndpoints_WithUnsafeDbContext_ShouldReportMTI001()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Endpoints
{
    // Using UNSAFE DbContext directly in minimal endpoint
    public sealed class GetUnsafeProjects
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/unsafe/projects"",
                async (TestApp.Data.UnsafeDbContext dbContext) =>
                {
                    // Should trigger MTI001 - direct access to tenant-isolated entity through unsafe DbContext
                    var projects = await dbContext.Projects.ToListAsync();
                    return Results.Ok(projects);
                });
        }
    }
}";
		var expected = new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
			.WithSpan(146, 42, 146, 60)
			.WithArguments("Project");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region Scenario 10: Using regular classes with SAFE repository

	[Fact]
	public async Task Scenario10_RegularClasses_WithSafeRepository_ShouldNotReportMTI001()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}

namespace TestApp.Repositories
{
    public sealed class SafeRepository : Multitenant.Enforcer.EntityFramework.TenantRepository<TestApp.Entities.Project, TestApp.Data.UnsafeDbContext>
    {
        public SafeRepository(TestApp.Data.UnsafeDbContext context,
                             Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor,
                             ILogger<SafeRepository> logger)
            : base(context, tenantAccessor, logger) { }

        public async Task<TestApp.Entities.Project?> GetByIdAsync(Guid id)
        {
            return await GetByIdAsync(id, cancellationToken: default);
        }

        public async Task<List<TestApp.Entities.Project>> GetProjectsAsync()
        {
            return await Query().AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        }
    }
}

namespace TestApp.Services
{
    public class ProjectService
    {
        private readonly TestApp.Repositories.SafeRepository _repository;
        
        public ProjectService(TestApp.Repositories.SafeRepository repository)
        {
            _repository = repository;
        }
        
        public async Task<TestApp.Entities.Project?> GetProjectByIdAsync(Guid id)
        {
            // Should NOT trigger MTI001 because we're using a safe repository
            return await _repository.GetByIdAsync(id);
        }
        
        public async Task<List<TestApp.Entities.Project>> GetAllProjectsAsync()
        {
            // Should NOT trigger MTI001 because we're using a safe repository
            return await _repository.GetProjectsAsync();
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Additional Edge Cases

	[Fact]
	public async Task EdgeCase_MixedEntityTypes_OnlyTenantIsolatedShouldTrigger()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class MixedDbContext : DbContext
    {
        public MixedDbContext(DbContextOptions<MixedDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Company> Companies { get; set; }  // Not tenant-isolated
        public DbSet<TestApp.Entities.Project> Projects { get; set; }    // Tenant-isolated
        public DbSet<TestApp.Entities.AdminAuditLog> AuditLogs { get; set; }  // Not tenant-isolated
    }
}

namespace TestApp.Services
{
    public class MixedService
    {
        public async Task TestMixedAccess(TestApp.Data.MixedDbContext context)
        {
            // Should NOT trigger MTI001 - Company is not tenant-isolated
            var companies = await context.Companies.ToListAsync();
            
            // Should trigger MTI001 - Project is tenant-isolated
            var projects = await context.Projects.ToListAsync();
            
            // Should NOT trigger MTI001 - AdminAuditLog is not tenant-isolated
            var logs = await context.AuditLogs.ToListAsync();
            
            // Should trigger MTI001 - Project is tenant-isolated via Set<T>()
            var projectsViaSet = await context.Set<TestApp.Entities.Project>().ToListAsync();
            
            // Should NOT trigger MTI001 - Company is not tenant-isolated via Set<T>()
            var companiesViaSet = await context.Set<TestApp.Entities.Company>().ToListAsync();
        }
    }
}";
		var expected = new[]
		{
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(142, 34, 142, 50)
				.WithArguments("Project"),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(148, 40, 148, 77)
				.WithArguments("Project")
		};

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task EdgeCase_NestedAccess_ShouldTrigger()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }
    }
}

namespace TestApp.Services
{
    public class NestedService
    {
        public async Task TestNestedAccess(TestApp.Data.UnsafeDbContext context)
        {
            // Should trigger MTI001 - nested access to tenant-isolated entity
            var result = await context.Projects.Where(p => p.Name.Contains(""test"")).FirstOrDefaultAsync();
        }
    }
}";
		var expected = new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
			.WithSpan(137, 32, 137, 48)
			.WithArguments("Project");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region Helper Methods

	private static async Task VerifyAnalyzerAsync(string testCode, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<DirectDbAccessAnalyzer, XUnitVerifier>
		{
			TestCode = testCode,
		};

		// Use .NET 8 reference assemblies and add EF Core + ASP.NET Core packages
		test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80
			.AddPackages([
				new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.0"),
				new PackageIdentity("Microsoft.EntityFrameworkCore.Relational", "8.0.0"),
				new PackageIdentity("Microsoft.AspNetCore.App.Ref", "8.0.0")
			]);

		test.ExpectedDiagnostics.AddRange(expected);

		await test.RunAsync();
	}

	#endregion
}
