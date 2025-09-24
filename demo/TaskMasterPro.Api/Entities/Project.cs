using Knara.MultiTenant.IsolationEnforcer.EntityFramework;

namespace TaskMasterPro.Api.Entities;

public class Project : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid ProjectManagerId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
	public DateTime CreatedAt { get; set; }
	public User ProjectManager { get; set; } = null!;
	public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
}

public enum ProjectStatus
{
	Planning = 1,
	Active = 2,
	OnHold = 3,
	Completed = 4,
	Cancelled = 5
}
