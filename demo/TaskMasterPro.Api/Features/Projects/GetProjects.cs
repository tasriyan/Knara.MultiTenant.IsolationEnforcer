using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public sealed class GetProjects : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects/{filter}",
			async (string? filter,
					IProjectRepository projectRepository,
					ITenantContextAccessor tenantAccessor,
					ILogger<GetProjects> logger,
					CurrentUserService userSvc) =>
			{
				var projects = await projectRepository.GetProjectsAsync(filter ?? "all");

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

// This is an example of bad endpoint implementation that uses UnsafeDbContext directly, that could violate tenant isolation
//public sealed class TenantIsolationViolationEndpoint : IEndpoint
//{
//	public void AddEndpoint(IEndpointRouteBuilder app)
//	{

//		app.MapGet("/api/projects/{filter}",
//			async (string? filter,
//					UnsafeDbContext dbContext,
//					ITenantContextAccessor tenantAccessor,
//					ILogger<GetProjects> logger,
//					CurrentUserService userSvc) =>
//			{
//				var projects = await dbContext.Projects.Where(p => p.TenantId == tenantAccessor.Current.TenantId)
//					.ToListAsync();

//				logger.LogInformation("Retrieved {Count} projects for tenant {TenantId}",
//					projects.Count, tenantAccessor.Current.TenantId);

//				return Results.Ok(projects.Select(p =>
//									new ProjectResponse(p.Id, p.TenantId, p.Name,
//														p.Description, p.ProjectManagerId, p.StartDate,
//														p.EndDate, p.Status, p.CreatedAt))
//								.ToList());
//			})
//		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
//	}
//}
