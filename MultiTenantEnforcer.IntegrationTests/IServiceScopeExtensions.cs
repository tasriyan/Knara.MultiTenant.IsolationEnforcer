using Microsoft.Extensions.DependencyInjection;
using Multitenant.Enforcer.Core;
using MultiTenant.Enforcer.EntityFramework;

namespace MultiTenantEnforcer.IntegrationTests;

public static class IServiceScopeExtensions
{
	public static UnsafeTestDbContext GetDbContext(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<UnsafeTestDbContext>();
	}

	public static ITenantRepository<TestEntity> GetRepository(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<ITenantRepository<TestEntity>>();
	}

	public static ITenantContextAccessor GetTenantAccessor(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
	}

	public static void SetTenantContext(this IServiceScope scope, Guid tenantId, string source = "Test")
	{
		var tenantAccessor = GetTenantAccessor(scope);
		tenantAccessor.SetContext(TenantContext.ForTenant(tenantId, source));
	}

	public static void SetSystemContext(this IServiceScope scope, string source = "SystemTest")
	{
		var tenantAccessor = GetTenantAccessor(scope);
		tenantAccessor.SetContext(TenantContext.SystemContext(source));
	}
}
