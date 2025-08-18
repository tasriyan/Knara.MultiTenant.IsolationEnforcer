using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

public class TenantIsolationAnalyzerTests
{
	private static readonly string TestAssemblyReferences = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Multitenant.Enforcer.Core
{
    public interface ITenantIsolated { }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AllowCrossTenantAccessAttribute : Attribute
    {
        public AllowCrossTenantAccessAttribute(string justification, string role = null) { }
    }
}

namespace Multitenant.Enforcer.AspnetCore
{
    public interface ICrossTenantOperationManager
    {
        Task<T> ExecuteCrossTenantOperationAsync<T>(Func<Task<T>> operation, string operationDescription);
        Task ExecuteCrossTenantOperationAsync(Func<Task> operation, string operationDescription);
    }
    
    public static class TenantContext
    {
        public static IDisposable SystemContext() => null;
    }
}

namespace TestApp.Entities
{
    public class TenantIsolatedEntity : Multitenant.Enforcer.Core.ITenantIsolated
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
    
    public class NonTenantEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}

namespace TestApp.Data
{
    public class TestDbContext : DbContext
    {
        public DbSet<TestApp.Entities.TenantIsolatedEntity> TenantEntities { get; set; }
        public DbSet<TestApp.Entities.NonTenantEntity> NonTenantEntities { get; set; }
    }
}
";

	#region MTI001 - DirectDbSetAccess Tests

	[Fact]
	public async Task MTI001_DirectDbSetPropertyAccess_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod(TestApp.Data.TestDbContext context)
        {
            var entities = context.TenantEntities.ToList();
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
			.WithSpan(54, 28, 54, 50)
			.WithArguments("TenantIsolatedEntity");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI001_DirectDbSetMethodAccess_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod(TestApp.Data.TestDbContext context)
        {
            var entities = context.Set<TestApp.Entities.TenantIsolatedEntity>().ToList();
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
			.WithSpan(54, 28, 54, 85)
			.WithArguments("TenantIsolatedEntity");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI001_NonTenantEntityAccess_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod(TestApp.Data.TestDbContext context)
        {
            var entities = context.NonTenantEntities.ToList();
            var entities2 = context.Set<TestApp.Entities.NonTenantEntity>().ToList();
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region MTI002 - MissingCrossTenantAttribute Tests

	[Fact]
	public async Task MTI002_MethodWithCrossTenantManager_WithoutAttribute_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public async Task TestMethod(Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager)
        {
            await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                // Cross-tenant operation
            }, ""Test operation"");
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
			.WithSpan(52, 27, 52, 37);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI002_MethodWithCrossTenantManager_WithMethodAttribute_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""Test justification"", ""SystemAdmin"")]
        public async Task TestMethod(Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager)
        {
            await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                // Cross-tenant operation
            }, ""Test operation"");
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	[Fact]
	public async Task MTI002_ClassWithCrossTenantManager_WithoutAttribute_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestEndpoint
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/test"", async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager) =>
            {
                return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                {
                    return Results.Ok();
                }, ""Test operation"");
            });
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
			.WithSpan(54, 32, 61, 15);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI002_ClassWithCrossTenantManager_WithClassAttribute_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""Test justification"", ""SystemAdmin"")]
    public class TestEndpoint
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/test"", async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager) =>
            {
                return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                {
                    return Results.Ok();
                }, ""Test operation"");
            });
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region MTI003 - PotentialFilterBypass Tests

	[Fact]
	public async Task MTI003_IgnoreQueryFilters_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod(TestApp.Data.TestDbContext context)
        {
            var entities = context.TenantEntities.IgnoreQueryFilters().ToList();
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.PotentialFilterBypass)
			.WithSpan(54, 51, 54, 70)
			.WithArguments("IgnoreQueryFilters()");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI003_FromSqlRaw_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod(TestApp.Data.TestDbContext context)
        {
            var entities = context.TenantEntities.FromSqlRaw(""SELECT * FROM TenantEntities"").ToList();
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.PotentialFilterBypass)
			.WithSpan(54, 51, 54, 97)
			.WithArguments("FromSqlRaw");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region MTI004 - TenantEntityWithoutRepository Tests

	[Fact]
	public async Task MTI004_DbSetGenericUsage_OutsideRepository_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod()
        {
            DbSet<TestApp.Entities.TenantIsolatedEntity> dbSet = null;
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.TenantEntityWithoutRepository)
			.WithSpan(53, 13, 53, 60)
			.WithArguments("TenantIsolatedEntity");

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI004_DbSetGenericUsage_InsideRepository_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestRepository
    {
        public void TestMethod()
        {
            DbSet<TestApp.Entities.TenantIsolatedEntity> dbSet = null;
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region MTI005 - UnauthorizedSystemContext Tests

	[Fact]
	public async Task MTI005_SystemContextUsage_WithoutAttribute_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        public void TestMethod()
        {
            using var context = Multitenant.Enforcer.AspnetCore.TenantContext.SystemContext();
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.UnauthorizedSystemContext)
			.WithSpan(52, 21, 52, 31);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task MTI005_SystemContextUsage_WithAttribute_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public class TestService
    {
        [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""System admin needs system context"", ""SystemAdmin"")]
        public void TestMethod()
        {
            using var context = Multitenant.Enforcer.AspnetCore.TenantContext.SystemContext();
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	#endregion

	#region Complex Scenarios Tests

	[Fact]
	public async Task ComplexScenario_MinimalAPI_WithAttribute_ShouldNotReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""System admin needs to view all companies"", ""SystemAdmin"")]
    public sealed class GetAllCompanies
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/admin/companies"",
                async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager,
                        TestApp.Data.TestDbContext context) =>
                {
                    return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                    {
                        var companies = await context.NonTenantEntities
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

	[Fact]
	public async Task ComplexScenario_MinimalAPI_WithoutAttribute_ShouldReportDiagnostic()
	{
		var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    public sealed class GetAuditLogs
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/admin/audit-logs"",
                async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager,
                        TestApp.Data.TestDbContext context) =>
                {
                    return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                    {
                        var logs = await context.NonTenantEntities
                            .OrderByDescending(log => log.Id)
                            .Take(100)
                            .ToListAsync();

                        return Results.Ok(logs);
                    }, ""Admin viewing audit logs"");
                });
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
			.WithSpan(55, 16, 66, 19);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	#endregion

	#region Helper Methods

	private static async Task VerifyAnalyzerAsync(string testCode, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<TenantIsolationAnalyzer, XUnitVerifier>
		{
			TestCode = testCode,
		};

		// Add necessary references
		test.ReferenceAssemblies = test.ReferenceAssemblies
			.AddPackages([
				new PackageIdentity("Microsoft.EntityFrameworkCore", "9.0.8"),
				new PackageIdentity("Microsoft.AspNetCore.App.Ref", "9.0.8")
			]);

		test.ExpectedDiagnostics.AddRange(expected);

		await test.RunAsync();
	}

	#endregion
}