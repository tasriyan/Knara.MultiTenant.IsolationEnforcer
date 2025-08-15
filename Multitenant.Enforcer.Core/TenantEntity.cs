namespace Multitenant.Enforcer.Core;

public class TenantEntity
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Domain { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; }
}
