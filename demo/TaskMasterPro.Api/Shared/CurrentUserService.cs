using System.Security.Claims;

namespace TaskMasterPro.Api.Shared;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
	public string? UserId =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue("nameidentifier") ?? "system";
	public string? UserName =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

	public string? UserEmail =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email) ?? "system";

	public string? IpAddress =>
		_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}