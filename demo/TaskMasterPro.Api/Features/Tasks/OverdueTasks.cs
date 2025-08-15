using Microsoft.CodeAnalysis;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class OverdueTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/overdue",
			async (ITaskRepository repository) =>
			{
				var tasks = await repository.GetOverdueTasksAsync();

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
