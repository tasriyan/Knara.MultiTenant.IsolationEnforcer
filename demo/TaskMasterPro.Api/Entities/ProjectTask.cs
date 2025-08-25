using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;

namespace TaskMasterPro.Api.Entities;

public class ProjectTask : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public Guid ProjectId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid? AssignedToId { get; set; }
	public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.Medium;
	public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.ToDo;
	public DateTime? DueDate { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public Project Project { get; set; } = null!;
	public User? AssignedTo { get; set; }
	public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}

public enum ProjectTaskPriority
{
	Low = 1,
	Medium = 2,
	High = 3,
	Critical = 4
}

public enum ProjectTaskStatus
{
	ToDo = 1,
	InProgress = 2,
	InReview = 3,
	Done = 4
}
