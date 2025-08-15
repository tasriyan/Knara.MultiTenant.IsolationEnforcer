using Microsoft.AspNetCore.Authorization;

namespace TaskMasterPro.Api.Shared;

public static class AuthorizationPolicies
{
	public static AuthorizationPolicy HasWriteActionPolicy { get; }
		= new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("scope", "read", "write")
		.Build();

	public static AuthorizationPolicy HasReadActionPolicy { get; }
		= new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("scope", "read")
		.Build();

	public static AuthorizationPolicy ProjectManager { get; }
		= new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("role", "ProjectManager", "Admin")
		.Build();
}