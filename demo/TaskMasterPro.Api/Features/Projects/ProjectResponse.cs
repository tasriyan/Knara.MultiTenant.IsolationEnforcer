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
