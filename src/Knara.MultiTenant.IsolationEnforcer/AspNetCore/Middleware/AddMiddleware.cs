using Microsoft.AspNetCore.Builder;

namespace Knara.MultiTenant.IsolationEnforcer.AspNetCore.Middleware;

public static class AddMiddleware
{
	public static IApplicationBuilder UseMultiTenantIsolation(this IApplicationBuilder app)
	{
		return app.UseMiddleware<TenantContextMiddleware>();
	}
}
