using Microsoft.AspNetCore.Builder;

namespace Multitenant.Enforcer.AspnetCore;

public static class AddMiddleware
{
	public static IApplicationBuilder UseMultiTenantIsolation(this IApplicationBuilder app)
	{
		return app.UseMiddleware<TenantContextMiddleware>();
	}
}
