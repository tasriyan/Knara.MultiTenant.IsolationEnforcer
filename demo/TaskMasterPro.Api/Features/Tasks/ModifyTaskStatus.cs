using MultiTenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.DataAccess;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Tasks;

public record UpdateTaskStatusDto(ProjectTaskStatus Status);

public sealed class ModifyTaskStatus : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapPut("/api/tasks/{id:guid}/status",
			async (
				Guid id,
				UpdateTaskStatusDto dto,
				TenantIsolatedRepository<ProjectTask, UnsafeDbContext> repository) =>
			{
				var task = await repository.GetByIdAsync(id);

				if (task == null)
				{
					return Results.NotFound();
				}

				task.Status = dto.Status;
				if (dto.Status == ProjectTaskStatus.Done)
				{
					task.CompletedAt = DateTime.UtcNow;
				}

				await repository.UpdateAsync(task);

				return Results.NoContent();
			})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy);
	}
}
