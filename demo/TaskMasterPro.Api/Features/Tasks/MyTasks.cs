using Microsoft.CodeAnalysis;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class GetMyTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/my-tasks",
			async (ITaskRepository repository,
					CurrentUserService userSvc) =>
			{
				var userId = Guid.Parse(userSvc.UserId);
				var tasks = await repository.GetTasksByUserAsync(userId);

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
