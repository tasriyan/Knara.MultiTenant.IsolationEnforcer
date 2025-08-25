using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;
using System.Security.Claims;

namespace Multitenant.Enforcer.AspnetCore;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
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

	public string? UserAgent =>
		_httpContextAccessor.HttpContext?.Request.Headers["UserAgent"].FirstOrDefault() ?? "unknown";

	public string? RequestId =>
		_httpContextAccessor.HttpContext?.TraceIdentifier ?? "unknown";

	public bool IsAuthenticated =>
		_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

	public IEnumerable<string> UserRoles =>
		_httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? Enumerable.Empty<string>();
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
