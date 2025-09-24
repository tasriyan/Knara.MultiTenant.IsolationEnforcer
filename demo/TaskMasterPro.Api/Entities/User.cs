using Knara.MultiTenant.IsolationEnforcer.EntityFramework;

namespace TaskMasterPro.Api.Entities;

public class User : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public string Email { get; set; } = string.Empty;
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public UserRole Role { get; set; } = UserRole.Member;
	public DateTime CreatedAt { get; set; }
	public bool IsActive { get; set; } = true;
	public string FullName => $"{FirstName} {LastName}";
}

public enum UserRole
{
	Member = 1,
	ProjectManager = 2,
	Admin = 3
}
