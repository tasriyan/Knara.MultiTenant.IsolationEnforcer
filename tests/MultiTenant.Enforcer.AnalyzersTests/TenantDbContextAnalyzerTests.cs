using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

/// <summary>
/// Tests for TenantDbContextAnalyzer (MTI006) covering scenarios from roslyn_tests_scenarios.md
/// </summary>
public class TenantDbContextAnalyzerTests
{
	private const string BaseTestCode = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
}

namespace Multitenant.Enforcer.EntityFramework
{
    public abstract class TenantDbContext : DbContext
    {
        protected TenantDbContext(DbContextOptions options, Multitenant.Enforcer.Core.ITenantContextAccessor tenantAccessor, ILogger logger) : base(options) { }
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

	#region Scenario 3 & 6: UNSAFE DbContext with ITenantIsolated entities - Should Report MTI006

	[Fact]
	public async Task UnsafeDbContext_WithTenantIsolatedEntities_ShouldReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // UNSAFE: Contains tenant-isolated entities but not derived from TenantDbContext
    // Should trigger MTI006 because it directly inherits from DbContext and has tenant-isolated entities
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

		var expected = new DiagnosticResult(DiagnosticDescriptors.DbContextMustInheritTenantDbContext)
			.WithSpan(93, 18, 93, 33)
			.WithArguments("UnsafeDbContext");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task UnsafeDbContext_WithMultipleTenantIsolatedEntities_ShouldReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // Additional tenant-isolated entity for testing
    public class Task : Multitenant.Enforcer.Core.ITenantIsolated
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    // UNSAFE: Contains multiple tenant-isolated entities but not derived from TenantDbContext
    public class UnsafeDbContext : DbContext
    {
        public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }
        public DbSet<TestApp.Data.Task> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.DbContextMustInheritTenantDbContext)
			.WithSpan(100, 18, 100, 33)
			.WithArguments("UnsafeDbContext");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task UnsafeDbContext_WithMixedEntityTypes_ShouldReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // UNSAFE: Contains both tenant-isolated and non-tenant-isolated entities
    // Should still trigger MTI006 because it has at least one tenant-isolated entity
    public class MixedDbContext : DbContext
    {
        public MixedDbContext(DbContextOptions<MixedDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Company> Companies { get; set; }  // Not tenant-isolated
        public DbSet<TestApp.Entities.Project> Projects { get; set; }    // Tenant-isolated
        public DbSet<TestApp.Entities.AdminAuditLog> AuditLogs { get; set; }  // Not tenant-isolated

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.CompanyConfiguration());
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.AdminAuditLogConfiguration());
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.DbContextMustInheritTenantDbContext)
			.WithSpan(93, 18, 93, 32)
			.WithArguments("MixedDbContext");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region Scenario 1: SAFE DbContext derived from TenantDbContext - Should NOT Report MTI006

	[Fact]
	public async Task SafeDbContext_DerivedFromTenantDbContext_ShouldNotReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // SAFE: Derived from TenantDbContext
    // Should NOT trigger MTI006 because it inherits from TenantDbContext
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
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Scenario 2: SAFE DbContext without ITenantIsolated entities - Should NOT Report MTI006

	[Fact]
	public async Task SafeDbContext_NoTenantIsolatedEntities_ShouldNotReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // SAFE: No tenant-isolated entities
    // Should NOT trigger MTI006 because it has no tenant-isolated entities
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
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task DbContext_InheritingFromCustomDbContext_ShouldNotReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // Custom base DbContext that inherits from EF's DbContext
    public abstract class CustomDbContext : DbContext
    {
        protected CustomDbContext(DbContextOptions options) : base(options) { }
    }

    // Should NOT trigger MTI006 because it doesn't directly inherit from DbContext
    public class IndirectDbContext : CustomDbContext
    {
        public IndirectDbContext(DbContextOptions<IndirectDbContext> options) : base(options) { }
        
        public DbSet<TestApp.Entities.Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TestApp.Configurations.ProjectConfiguration());
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	[Fact]
	public async Task EmptyDbContext_DirectlyInheritingFromDbContext_ShouldNotReportMTI006()
	{
		var testCode = BaseTestCode + @"
namespace TestApp.Data
{
    // Should NOT trigger MTI006 because it has no DbSet properties at all
    public class EmptyDbContext : DbContext
    {
        public EmptyDbContext(DbContextOptions<EmptyDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // No entities configured
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Helper Methods

	private static async Task VerifyAnalyzerAsync(string testCode, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<TenantDbContextAnalyzer, XUnitVerifier>
		{
			TestCode = testCode,
		};

		// Use .NET 8 reference assemblies and add EF Core packages
		test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80
			.AddPackages([
				new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.0"),
				new PackageIdentity("Microsoft.EntityFrameworkCore.Relational", "8.0.0")
			]);

		test.ExpectedDiagnostics.AddRange(expected);

		await test.RunAsync();
	}

	#endregion
}
