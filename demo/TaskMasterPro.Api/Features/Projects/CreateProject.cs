using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public record CreateProjectRequest(
	string Name,
	string Description,
	Guid ProjectManagerId,
	DateTime StartDate,
	DateTime? EndDate
);

public sealed class CreateProject : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapPost("/api/projects",
			async (CreateProjectRequest request,
					IProjectRepository projectRepository,
					ITenantContextAccessor tenantAccessor,
					ILogger<GetProject> logger,
					CurrentUserService userSvc) =>
			{
				var project = new Project
				{
					Id = Guid.NewGuid(),
					Name = request.Name,
					Description = request.Description,
					ProjectManagerId = request.ProjectManagerId,
					StartDate = request.StartDate,
					EndDate = request.EndDate,
					CreatedAt = DateTime.UtcNow
					// TenantId is automatically set by the TenantDbContext
				};

				await projectRepository.AddAsync(project);

				logger.LogInformation("Created project {ProjectId} for tenant {TenantId}",
					project.Id, tenantAccessor.Current.TenantId);

				var createdProject = await projectRepository.GetByIdAsync(project.Id);
				if (createdProject == null)
				{
					logger.LogError("Failed to retrieve created project {ProjectId} for tenant {TenantId}",
						project.Id, tenantAccessor.Current.TenantId);
					return Results.StatusCode(StatusCodes.Status500InternalServerError);
				}

				return Results.CreatedAtRoute(nameof(GetProject), new { id = project.Id },
					new ProjectResponse(
						createdProject.Id,
						createdProject.TenantId,
						createdProject.Name,
						createdProject.Description,
						createdProject.ProjectManagerId,
						createdProject.StartDate,
						createdProject.EndDate,
						createdProject.Status,
						createdProject.CreatedAt
						));
			})
		.RequireAuthorization(AuthorizationPolicies.ProjectManager);
	}
}

	// This method should be caught by analyzer
	/* ANALYZER VIOLATION EXAMPLE:
    [HttpGet("bad-example")]
    public async Task<ActionResult> BadExample()
    {
        // This would trigger MTI001 error: Direct DbSet access on tenant-isolated entity
        var projects = await _context.Set<Project>().ToListAsync();
        return Ok(projects);
    }
    */
