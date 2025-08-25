namespace Multitenant.Enforcer.Core;

public interface ICurrentUserService
{
	string? UserId { get; }
	string? UserName { get; }
	string? UserEmail { get; }
	string? IpAddress { get; }
	string? UserAgent { get; }
	string? RequestId { get; }
	bool IsAuthenticated { get; }
	IEnumerable<string> UserRoles { get; }
}
