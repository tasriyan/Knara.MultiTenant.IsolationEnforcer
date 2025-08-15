using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Projects;

public sealed class CreateProject : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapPost("/api/projects",
			async (Guid id,
					IProjectRepository projectRepository,
					ITenantContextAccessor tenantAccessor,
					ILogger<GetProject> logger,
					CurrentUserService userSvc) =>
			{
				var project = new Project
				{
					Id = Guid.NewGuid(),
					Name = dto.Name,
					Description = dto.Description,
					ProjectManagerId = dto.ProjectManagerId,
					StartDate = dto.StartDate,
					EndDate = dto.EndDate,
					CreatedAt = DateTime.UtcNow
					// TenantId is automatically set by the TenantDbContext
				};

				await projectRepository.AddAsync(project);

				logger.LogInformation("Created project {ProjectId} for tenant {TenantId}",
					project.Id, tenantAccessor.Current.TenantId);

				return Results.CreatedAtRoute(nameof(GetProject), new { id = project.Id },
					ProjectDto.FromEntity(project));
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
