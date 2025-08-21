using Multitenant.Enforcer.Core;

namespace TaskMasterPro.Api.Entities;

public class Company
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Domain { get; set; } = string.Empty; 
	public DateTime CreatedAt { get; set; }
	public CompanyTier Tier { get; set; } = CompanyTier.Starter;
	public bool IsActive { get; set; } = true;
}

public enum CompanyTier
{
	Starter = 1,
	Professional = 2,
	Enterprise = 3
}
