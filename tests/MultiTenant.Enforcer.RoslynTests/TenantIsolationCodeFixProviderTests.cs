using Microsoft.CodeAnalysis.Testing;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

public class TenantIsolationCodeFixProviderTests
{
    private static readonly string TestAssemblyReferences = @"
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Multitenant.Enforcer.Core;

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
}

namespace TestApp.Data
{
    public class TestDbContext : DbContext
    {
        public DbSet<TestApp.Entities.TenantIsolatedEntity> TenantEntities { get; set; }
    }
}
";

    #region MTI002 Code Fix Tests

    [Fact]
    public async Task MTI002_MethodWithoutAttribute_ShouldAddMethodAttribute()
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

        var fixedCode = TestAssemblyReferences + @"


namespace TestApp
{
    public class TestService
    {
        [AllowCrossTenantAccess(""TODO: Provide business justification for cross-tenant access"", ""SystemAdmin"")]
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
            .WithSpan(45, 27, 45, 37);

        await Helpers.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

	[Fact]
	public async Task MTI002_ClassWithLambda_ShouldAddClassAttribute()
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

		var fixedCode = TestAssemblyReferences + @"
namespace TestApp
{
    [AllowCrossTenantAccess(""TODO: Provide business justification for cross-tenant access"", ""SystemAdmin"")]
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
			.WithSpan(50, 4, 70, 100); // Only the lambda expression

		await Helpers.VerifyCodeFixAsync(testCode, expected, fixedCode);
	}

	[Fact]
    public async Task MTI002_ClassAlreadyHasAttribute_ShouldNotModify()
    {
        var testCode = TestAssemblyReferences + @"
namespace TestApp
{
    [AllowCrossTenantAccess(""Already authorized"", ""SystemAdmin"")]
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

        // No diagnostic should be reported since the class already has the attribute
        await Helpers.VerifyCodeFixAsync(testCode);
    }

    [Fact]
    public async Task MTI002_MethodAlreadyHasAttribute_ShouldNotModify()
    {
        var testCode = TestAssemblyReferences + @"
using Multitenant.Enforcer.Core;

namespace TestApp
{
    public class TestService
    {
        [AllowCrossTenantAccess(""Already authorized"", ""SystemAdmin"")]
        public async Task TestMethod(Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager)
        {
            await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                // Cross-tenant operation
            }, ""Test operation"");
        }
    }
}";

        // No diagnostic should be reported since the method already has the attribute
        await Helpers.VerifyCodeFixAsync(testCode);
    }

    [Fact]
    public async Task MTI002_UsingStatementAlreadyExists_ShouldNotDuplicate()
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

        var fixedCode = TestAssemblyReferences + @"


namespace TestApp
{
    public class TestService
    {
        [AllowCrossTenantAccess(""TODO: Provide business justification for cross-tenant access"", ""SystemAdmin"")]
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
            .WithSpan(47, 27, 47, 37);

        await Helpers.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    [Fact]
    public async Task MTI002_ComplexMinimalAPIScenario_ShouldAddClassAttribute()
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
                        var logs = await context.TenantEntities
                            .OrderByDescending(log => log.Id)
                            .Take(100)
                            .ToListAsync();

                        return Results.Ok(logs);
                    }, ""Admin viewing audit logs"");
                });
        }
    }
}";

        var fixedCode = TestAssemblyReferences + @"


namespace TestApp
{
    [AllowCrossTenantAccess(""TODO: Provide business justification for cross-tenant access"", ""SystemAdmin"")]
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
                        var logs = await context.TenantEntities
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
            .WithSpan(48, 16, 59, 19);

        await Helpers.VerifyCodeFixAsync(testCode, expected, fixedCode);
    }

    #endregion
}	