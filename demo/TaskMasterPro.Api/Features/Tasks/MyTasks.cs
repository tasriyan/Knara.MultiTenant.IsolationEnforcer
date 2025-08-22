using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class GetMyTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/my-tasks",
			async (TenantRepository<ProjectTask, UnsafeDbContext> repository,
					ICurrentUserService userSvc) =>
			{
				if (!Guid.TryParse(userSvc!.UserId, out var userId))
					return Results.BadRequest("User id not provided.");

				var tasks = await repository.Query()
								.AsNoTracking()
								.Include(t => t.Project)
								.Where(t => t.AssignedToId == userId)
								.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
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
