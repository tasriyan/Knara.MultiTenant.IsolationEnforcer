using Microsoft.AspNetCore.Http;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

/// <summary>
/// Interface for resolving tenant context from HTTP requests.
/// Implement this interface for different tenant identification strategies.
/// </summary>
public interface ITenantResolver
{
	/// <summary>
	/// Resolves the tenant context from the HTTP request.
	/// </summary>
	/// <param name="context">The HTTP context</param>
	/// <returns>The resolved tenant context</returns>
	Task<TenantContext> ResolveTenantAsync(HttpContext context);
}
