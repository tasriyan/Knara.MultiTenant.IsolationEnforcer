using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Features.Projects;

public record ProjectResponse(Guid Id,
	Guid TenantId,
	string Name,
	string Description,
	Guid ProjectManagerId,
	DateTime StartDate,
	DateTime? EndDate,
	ProjectStatus Status,
	DateTime CreatedAt
);

public record ProjectWithTasksResponse(
	Guid Id,
	Guid TenantId,
	string Name,
	string Description,
	Guid ProjectManagerId,
	DateTime StartDate,
	DateTime? EndDate,
	ProjectStatus Status,
	DateTime CreatedAt,
	IEnumerable<ProjectTaskResponse> Tasks
);

public record ProjectTaskResponse(
	Guid Id,
	Guid TenantId,
	string Name,
	string Description
);
