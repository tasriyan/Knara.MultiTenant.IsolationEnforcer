using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Features.Projects;
using TaskMasterPro.Api.Shared;

// File contains classes that were intentionally designed to not compile to test Roslyn analyzer behavior
// Uncomment to experience the compilation errors and warnings from the custom Roslyn analyzers
// ERRORS EXPECTED:
//		MTI001 - Direct DbSet access - Compilation error
//		MTI003 - Potential filter bypasses - Warning
//		MTI004 - Entities without repositories - Compilation error
//		MTI006 - DbContext not derived from TenantDbContext - Compilation error


// This class should not compile because
//	Because
//		it is directly derived from DbContext instead of TenantDbContext while using properties implementic ITenantIsolated
//  ERRORS EXPECTED: MTI006

namespace TaskMasterPro.Api.CodeAnalysisDemo;
public sealed class GetProjects : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects/{filter}",
			async (string? filter,
					ShouldNotCompileDBContext dbContext,
					ITenantContextAccessor tenantAccessor,
					ILogger<GetProjects> logger,
					CurrentUserService userSvc) =>
			{
				var projects = await dbContext.Projects.ToListAsync();

				logger.LogInformation("Retrieved {Count} projects for tenant {TenantId}",
					projects.Count, tenantAccessor.Current.TenantId);

				return Results.Ok(projects.Select(p =>
									new ProjectResponse(p.Id, p.TenantId, p.Name,
														p.Description, p.ProjectManagerId, p.StartDate,
														p.EndDate, p.Status, p.CreatedAt))
								.ToList());
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}
