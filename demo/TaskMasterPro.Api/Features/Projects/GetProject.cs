using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public record ProjectWithTasksResponse(
	Guid Id,
	Guid TenantId,
	string Name,
	string Description,
	Guid ProjectManagerId,
	DateTime StartDate,
	DateTime? EndDate,
	ProjectStatus Status,
	DateTime CreatedAt,
	IEnumerable<ProjectTaskResponse> Tasks
);

public record ProjectTaskResponse(
	Guid Id,
	Guid TenantId,
	string Name,
	string Description
);

public sealed class GetProject : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects/{id:guid}",
			async (Guid id,
					TenantIsolatedProjectRepositorySecondOption projectRepository,
					ILogger<GetProject> logger,
					ICurrentUserService userSvc) =>
			{
				var project = await projectRepository.GetProjectWithTasksAsync(id);

				if (project == null)
				{
					return Results.NotFound();
				}

				return Results.Ok(new ProjectWithTasksResponse(
					project.Id,
					project.TenantId,
					project.Name,
					project.Description,
					project.ProjectManagerId,
					project.StartDate,
					project.EndDate,
					project.Status,
					project.CreatedAt,
					project.Tasks.Select(t => new ProjectTaskResponse(
						t.Id,
						t.TenantId,
						t.Title,
						t.Description
					)).ToList()
					));
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
		.WithName("GetProject");
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
