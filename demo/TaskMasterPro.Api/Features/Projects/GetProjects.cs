using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public sealed class GetProjects : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects/active",
			async (IProjectRepository projectRepository,
					ITenantContextAccessor tenantAccessor,
					ILogger<GetProjects> logger,
					CurrentUserService userSvc) =>
			{
				var projects = await projectRepository.GetActiveProjectsAsync();

				logger.LogInformation("Retrieved {Count} projects for tenant {TenantId}",
					projects.Count, tenantAccessor.Current.TenantId);

				return Results.Ok(projects.Select(ProjectDto.FromEntity).ToList());
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
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
