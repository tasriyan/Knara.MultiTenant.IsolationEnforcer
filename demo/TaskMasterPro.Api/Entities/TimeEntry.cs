using Multitenant.Enforcer.Core;

namespace TaskMasterPro.Api.Entities;

public class TimeEntry : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public Guid TaskId { get; set; }
	public Guid UserId { get; set; }
	public DateTime StartTime { get; set; }
	public DateTime? EndTime { get; set; }
	public TimeSpan Duration { get; set; }
	public string Description { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public ProjectTask Task { get; set; } = null!;
	public User User { get; set; } = null!;
}

