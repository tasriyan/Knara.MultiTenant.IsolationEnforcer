using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Tasks;

public record ProjectTaskResponse(Guid Id, 
		Guid TenantId, 
		Guid ProjectId, 
		string Title, 
		string Description,
		ProjectTaskPriority Priority, 
		ProjectTaskStatus Status,
		DateTime? DueDate, 
		DateTime CreatedAt, 
		DateTime? CompletedAt, 
		ICollection<TimeEntry> TimeEntries);
