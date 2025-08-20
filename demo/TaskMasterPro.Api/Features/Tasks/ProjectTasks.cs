using Microsoft.CodeAnalysis;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class ProjectTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/project/{projectId:guid}",
			async (
				Guid projectId,
				ITasksDataAccess repository) =>
			{
				var tasks = await repository.GetTasksByProjectAsync(projectId);

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
