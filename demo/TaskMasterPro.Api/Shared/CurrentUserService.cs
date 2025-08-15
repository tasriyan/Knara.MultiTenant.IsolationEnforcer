using System.Security.Claims;

namespace TaskMasterPro.Api.Shared;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
	public string? UserId =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
	public string? UserName =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";
}
