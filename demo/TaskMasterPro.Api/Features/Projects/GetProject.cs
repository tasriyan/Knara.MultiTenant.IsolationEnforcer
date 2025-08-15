using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public sealed class GetProject : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/projects/{id:guid}",
			async (Guid id,
					IProjectRepository projectRepository,
					ILogger<GetProject> logger,
					CurrentUserService userSvc) =>
			{
				var project = await projectRepository.GetProjectWithTasksAsync(id);

				if (project == null)
				{
					return Results.NotFound();
				}

				return Results.Ok(ProjectDto.FromEntity(project));
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
