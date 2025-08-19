using Multitenant.Enforcer.Core;

namespace TaskMasterPro.Api.Entities;

public class Task : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public Guid ProjectId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Guid? AssignedToId { get; set; }
	public TaskPriority Priority { get; set; } = TaskPriority.Medium;
	public TaskStatus Status { get; set; } = TaskStatus.ToDo;
	public DateTime? DueDate { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? CompletedAt { get; set; }

	// Navigation properties
	public Project Project { get; set; } = null!;
	public User? AssignedTo { get; set; }
	public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}

public enum TaskPriority
{
	Low = 1,
	Medium = 2,
	High = 3,
	Critical = 4
}

public enum TaskStatus
{
	ToDo = 1,
	InProgress = 2,
	InReview = 3,
	Done = 4
}
