using Microsoft.EntityFrameworkCore;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class ProjectTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/project/{projectId:guid}",
			async (
				Guid projectId,
				TenantRepository<ProjectTask, UnsafeDbContext> repository) =>
			{
				var tasks = await repository.Query()
								.AsNoTracking()
								.Include(t => t.AssignedTo)
								.Where(t => t.ProjectId == projectId)
								.OrderBy(t => t.Priority)
								.ThenBy(t => t.DueDate)
								.ToListAsync();

				return Results.Ok(tasks
					.Select(t => new ProjectTaskResponse(t.Id,
					t.TenantId,
					t.ProjectId,
								t.Title,
								t.Description,
								t.Priority,
								t.Status,
								t.DueDate,
								t.CreatedAt,
								t.CompletedAt,
								t.TimeEntries))
					.ToList());
			})
		.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}
