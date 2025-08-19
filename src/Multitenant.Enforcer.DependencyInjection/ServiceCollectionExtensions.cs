using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.DomainResolvers;

namespace Multitenant.Enforcer.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static MultitenantIsolationBuilder AddMultiTenantIsolation(this IServiceCollection services,
		Action<MultiTenantOptions>? configure)
	{
		services.AddOptions<MultiTenantOptions>().Configure(opts =>
		{
			if (configure != null)
				configure?.Invoke(opts);
			else
				opts = MultiTenantOptions.DefaultOptions;
		});
		services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
		services.AddScoped<ICrossTenantOperationManager, CrossTenantOperationManager>();
		services.TryAddScoped<ITenantLookupService, TenantLookupService>();

		return new MultitenantIsolationBuilder(services);
	}
}
