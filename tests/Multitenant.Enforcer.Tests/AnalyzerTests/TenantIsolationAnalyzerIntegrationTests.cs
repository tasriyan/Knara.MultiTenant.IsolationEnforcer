using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Multitenant.Enforcer.Core.RoslynAnalyzers;

namespace MultiTenant.Enforcer.Tests.AnalyzerTests;

/// <summary>
/// Integration tests that test the analyzer against realistic code scenarios
/// similar to what would be found in actual applications.
/// </summary>
public class TenantIsolationAnalyzerIntegrationTests
{
	private const string BaseSetup = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Multitenant.Enforcer.Core
{
    public interface ITenantIsolated 
    {
        Guid TenantId { get; set; }
    }
    
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

namespace TaskMasterPro.Api.Entities
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public CompanyTier Tier { get; set; }
    }
    
    public enum CompanyTier { Basic, Premium, Enterprise }
    
    public class Task : Multitenant.Enforcer.Core.ITenantIsolated
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsCompleted { get; set; }
    }
    
    public class Project : Multitenant.Enforcer.Core.ITenantIsolated
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
    }
    
    public class AdminAuditLog
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Action { get; set; }
        public string UserEmail { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
    }
}

namespace TaskMasterPro.Data
{
    public class TaskMasterDbContext : DbContext
    {
        public DbSet<TaskMasterPro.Api.Entities.Company> Companies { get; set; }
        public DbSet<TaskMasterPro.Api.Entities.Task> Tasks { get; set; }
        public DbSet<TaskMasterPro.Api.Entities.Project> Projects { get; set; }
        public DbSet<TaskMasterPro.Api.Entities.AdminAuditLog> AdminAuditLogs { get; set; }
    }
}

namespace TaskMasterPro.Api.Shared
{
    public static class AuthorizationPolicies
    {
        public const string SystemAdmin = ""SystemAdmin"";
    }
    
    public interface IEndpoint
    {
        void AddEndpoint(IEndpointRouteBuilder app);
    }
    
    public class CurrentUserService
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
    }
}
";

	[Fact]
	public async Task RealWorldScenario_GetAllCompanies_WithAttribute_ShouldPass()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Features.Admin
{
    public record CompanyResponse(Guid Id, string Name, TaskMasterPro.Api.Entities.CompanyTier Tier);

    [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""System admin needs to view all companies"", ""SystemAdmin"")]
    public sealed class GetAllCompanies : TaskMasterPro.Api.Shared.IEndpoint
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/admin/companies"",
                async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager,
                        TaskMasterPro.Data.TaskMasterDbContext context,
                        TaskMasterPro.Api.Shared.CurrentUserService userSvc) =>
                {
                    return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                    {
                        var companies = await context.Companies
                                                .AsNoTracking()
                                                .OrderBy(c => c.Name)
                                                .ToListAsync();

                        return Results.Ok(companies.Select(c => new CompanyResponse(c.Id, c.Name, c.Tier)).ToList());
                    }, ""Admin viewing all companies"");
                })
            .RequireAuthorization(TaskMasterPro.Api.Shared.AuthorizationPolicies.SystemAdmin);
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	[Fact]
	public async Task RealWorldScenario_GetAuditLogs_WithoutAttribute_ShouldFail()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Features.Admin
{
    public record AdminAuditLogResponse(Guid Id, Guid TenantId, string Action, string UserEmail, string Details, DateTime Timestamp, string IpAddress);

    public sealed class GetAuditLogs : TaskMasterPro.Api.Shared.IEndpoint
    {
        public void AddEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(""/api/admin/audit-logs"",
                {|MTI002:async (Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager,
                        TaskMasterPro.Data.TaskMasterDbContext context,
                        TaskMasterPro.Api.Shared.CurrentUserService userSvc,
                        [FromQuery] Guid? tenantId = null,
                        [FromQuery] DateTime? fromDate = null,
                        [FromQuery] int take = 100) =>
                {
                    return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
                    {
                        var query = context.AdminAuditLogs.AsQueryable();

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

                        return Results.Ok(logs.Select(l => new AdminAuditLogResponse(
                            Id: l.Id,
                            TenantId: l.TenantId,
                            Action: l.Action,
                            UserEmail: l.UserEmail,
                            Details: l.Details,
                            Timestamp: l.Timestamp,
                            IpAddress: l.IpAddress))
                        .ToList());
                    }, $""Admin viewing audit logs for tenant {tenantId}"");
                }|})
            .RequireAuthorization(TaskMasterPro.Api.Shared.AuthorizationPolicies.SystemAdmin);
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
			.WithSpan(91, 16, 110, 19);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task RealWorldScenario_TaskService_DirectDbSetAccess_ShouldFail()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Services
{
    public class TaskService
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        
        public TaskService(TaskMasterPro.Data.TaskMasterDbContext context)
        {
            _context = context;
        }
        
        public async Task<List<TaskMasterPro.Api.Entities.Task>> GetAllTasksUnsafeAsync()
        {
            // This should trigger MTI001 because Tasks is tenant-isolated
            return await {|MTI001:_context.Tasks|}
                .Where(t => t.IsCompleted == false)
                .ToListAsync();
        }
        
        public async Task<List<TaskMasterPro.Api.Entities.Task>> GetTasksWithBypassAsync()
        {
            // This should trigger MTI003 because it bypasses query filters
            return await _context.Tasks
                .{|MTI003:IgnoreQueryFilters()|}
                .Where(t => t.IsCompleted == false)
                .ToListAsync();
        }
    }
}";

		var expected = new[]
		{
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(110, 42, 110, 56)
				.WithArguments("Task"),
			new DiagnosticResult(DiagnosticDescriptors.PotentialFilterBypass)
				.WithSpan(111, 17, 111, 36)
				.WithArguments("IgnoreQueryFilters()")
		};

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task RealWorldScenario_ProjectRepository_ShouldPass()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Repositories
{
    public interface ITenantRepository<T> where T : Multitenant.Enforcer.Core.ITenantIsolated
    {
        Task<List<T>> GetAllAsync();
        Task<T> GetByIdAsync(Guid id);
    }
    
    public class ProjectRepository : ITenantRepository<TaskMasterPro.Api.Entities.Project>
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        
        public ProjectRepository(TaskMasterPro.Data.TaskMasterDbContext context)
        {
            _context = context;
        }
        
        public async Task<List<TaskMasterPro.Api.Entities.Project>> GetAllAsync()
        {
            // This should be allowed in repository context
            return await _context.Projects
                .Where(p => p.CreatedDate >= DateTime.Now.AddMonths(-6))
                .ToListAsync();
        }
        
        public async Task<TaskMasterPro.Api.Entities.Project> GetByIdAsync(Guid id)
        {
            // Generic usage should be allowed in repository
            DbSet<TaskMasterPro.Api.Entities.Project> projects = _context.Projects;
            return await projects.FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	[Fact]
	public async Task RealWorldScenario_SystemContextUsage_WithoutAttribute_ShouldFail()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Services
{
    public class DataMigrationService
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        
        public DataMigrationService(TaskMasterPro.Data.TaskMasterDbContext context)
        {
            _context = context;
        }
        
        public async Task {|MTI005:MigrateDataAsync|}()
        {
            // This should trigger MTI005 because SystemContext requires authorization
            using var systemContext = Multitenant.Enforcer.AspnetCore.TenantContext.SystemContext();
            
            var allCompanies = await _context.Companies.ToListAsync();
            // Migration logic here...
        }
    }
}";

		var expected = new DiagnosticResult(DiagnosticDescriptors.UnauthorizedSystemContext)
			.WithSpan(138, 27, 138, 43);

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task RealWorldScenario_SystemContextUsage_WithAttribute_ShouldPass()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Services
{
    public class DataMigrationService
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        
        public DataMigrationService(TaskMasterPro.Data.TaskMasterDbContext context)
        {
            _context = context;
        }
        
        [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""Data migration requires system-wide access"", ""SystemAdmin"")]
        public async Task MigrateDataAsync()
        {
            // This should be allowed with proper authorization
            using var systemContext = Multitenant.Enforcer.AspnetCore.TenantContext.SystemContext();
            
            var allCompanies = await _context.Companies.ToListAsync();
            // Migration logic here...
        }
    }
}";

		await VerifyAnalyzerAsync(testCode);
	}

	[Fact]
	public async Task RealWorldScenario_MultipleViolationsInOneClass_ShouldReportAll()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Services
{
    public class BadAdminService
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        
        public BadAdminService(TaskMasterPro.Data.TaskMasterDbContext context)
        {
            _context = context;
        }
        
        public async Task {|MTI002:GetCrossTenantDataAsync|}(Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager)
        {
            // MTI002: Missing AllowCrossTenantAccess attribute
            await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                // MTI001: Direct access to tenant-isolated entity
                var tasks = await {|MTI001:_context.Tasks|}
                    .{|MTI003:IgnoreQueryFilters()|} // MTI003: Bypass query filters
                    .ToListAsync();
                
                // MTI001: Another direct access
                var projects = await {|MTI001:_context.Projects|}.ToListAsync();
                
                return tasks.Count + projects.Count;
            }, ""Getting cross-tenant data"");
            
            // MTI005: Unauthorized system context
            using var systemContext = Multitenant.Enforcer.AspnetCore.TenantContext.SystemContext();
        }
        
        public void BadGenericUsage()
        {
            // MTI004: Generic usage outside repository
            {|MTI004:DbSet<TaskMasterPro.Api.Entities.Task>|} taskSet = null;
            {|MTI004:IQueryable<TaskMasterPro.Api.Entities.Project>|} projectQuery = null;
        }
    }
}";

		var expected = new[]
		{
			new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
				.WithSpan(194, 27, 194, 49),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(200, 42, 200, 56)
				.WithArguments("Task"),
			new DiagnosticResult(DiagnosticDescriptors.PotentialFilterBypass)
				.WithSpan(201, 17, 201, 36)
				.WithArguments("IgnoreQueryFilters()"),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(205, 46, 205, 64)
				.WithArguments("Project"),
			new DiagnosticResult(DiagnosticDescriptors.UnauthorizedSystemContext)
				.WithSpan(194, 27, 194, 49),
			new DiagnosticResult(DiagnosticDescriptors.TenantEntityWithoutRepository)
				.WithSpan(212, 13, 212, 50)
				.WithArguments("Task"),
			new DiagnosticResult(DiagnosticDescriptors.TenantEntityWithoutRepository)
				.WithSpan(213, 13, 213, 56)
				.WithArguments("Project")
		};

		await VerifyAnalyzerAsync(testCode, expected);
	}

	[Fact]
	public async Task RealWorldScenario_ControllerWithMixedAccess_ShouldDetectViolations()
	{
		var testCode = BaseSetup + @"
namespace TaskMasterPro.Api.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route(""api/[controller]"")]
    public class AdminController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly TaskMasterPro.Data.TaskMasterDbContext _context;
        private readonly Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager _crossTenantManager;
        
        public AdminController(
            TaskMasterPro.Data.TaskMasterDbContext context,
            Multitenant.Enforcer.AspnetCore.ICrossTenantOperationManager crossTenantManager)
        {
            _context = context;
            _crossTenantManager = crossTenantManager;
        }
        
        [Microsoft.AspNetCore.Mvc.HttpGet(""companies"")]
        [Multitenant.Enforcer.Core.AllowCrossTenantAccess(""Admin needs to view all companies"", ""SystemAdmin"")]
        public async Task<IActionResult> GetCompanies()
        {
            // This should be allowed - has attribute and accesses non-tenant entity
            return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                var companies = await _context.Companies.ToListAsync();
                return Ok(companies);
            }, ""Admin viewing companies"");
        }
        
        [Microsoft.AspNetCore.Mvc.HttpGet(""tasks"")]
        public async Task<IActionResult> {|MTI002:GetAllTasks|}()
        {
            // MTI002: Missing attribute for cross-tenant operation
            return await _crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
            {
                // MTI001: Direct access to tenant-isolated entity
                var tasks = await {|MTI001:_context.Tasks|}
                    .{|MTI003:FromSqlRaw(""SELECT * FROM Tasks"")|} // MTI003: Raw SQL bypass
                    .ToListAsync();
                return Ok(tasks);
            }, ""Admin viewing all tasks"");
        }
    }
}";

		var expected = new[]
		{
			new DiagnosticResult(DiagnosticDescriptors.MissingCrossTenantAttribute)
				.WithSpan(281, 35, 281, 47),
			new DiagnosticResult(DiagnosticDescriptors.DirectDbSetAccess)
				.WithSpan(287, 42, 287, 56)
				.WithArguments("Task"),
			new DiagnosticResult(DiagnosticDescriptors.PotentialFilterBypass)
				.WithSpan(288, 17, 288, 62)
				.WithArguments("FromSqlRaw")
		};

		await VerifyAnalyzerAsync(testCode, expected);
	}

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