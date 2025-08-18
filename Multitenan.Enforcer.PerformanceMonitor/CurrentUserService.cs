using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace TaskMasterPro.Api.Shared;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
	public string? UserId =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
	public string? UserName =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) ?? "system";

	public string? UserEmail =>
		_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email) ?? "system";

	public string? IpAddress =>
		_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public static class PrincipalExtensions
{
	public static string? FindFirstValue(this ClaimsPrincipal principal, string claimType)
	{
		if (principal is null)
			throw new ArgumentNullException(nameof(principal), "ClaimsPrincipal cannot be null.");

		var claim = principal.FindFirst(claimType);
		return claim?.Value;
	}
}