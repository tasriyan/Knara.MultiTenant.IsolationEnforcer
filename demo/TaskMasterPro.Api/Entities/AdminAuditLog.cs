namespace TaskMasterPro.Api.Entities;

public class AdminAuditLog
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public string Action { get; set; } = string.Empty;
	public string EntityType { get; set; } = string.Empty;
	public Guid EntityId { get; set; }
	public Guid? UserId { get; set; }
	public string UserEmail { get; set; } = string.Empty;
	public string Details { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; }
	public string IpAddress { get; set; } = string.Empty;
}
