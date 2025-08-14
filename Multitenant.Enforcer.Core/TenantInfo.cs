namespace Multitenant.Enforcer.Core;

/// <summary>
/// Basic tenant information.
/// </summary>
public class TenantInfo
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Domain { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
