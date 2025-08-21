using Microsoft.EntityFrameworkCore;
using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public sealed class OverdueTasks : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapGet("/api/tasks/overdue",
			async (TenantRepository<ProjectTask, UnsafeDbContext> repository) =>
			{
				var today = DateTime.UtcNow.Date;
				var tasks = await repository.Query()
							.AsNoTracking()
							.Include(t => t.AssignedTo)
							.Include(t => t.Project)
							.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date < today)
							.Where(t => t.Status != ProjectTaskStatus.Done)
							.OrderBy(t => t.DueDate)
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
